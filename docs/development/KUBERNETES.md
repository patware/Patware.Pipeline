# Kubernetes deployment and verification

The local sample uses Docker Desktop Kubernetes, two Blazor frontends, two API/worker replicas, shared SQL Server, Redis, and Traefik. AppHost orchestrates local Aspire development; Kubernetes runs the API and Web containers directly.

The maintainer reported submissions through both API replicas and continued operation during API pod interruption. These checks are manual; the Aspire initial-rendering test does not automate Kubernetes fault injection.

## Prerequisites and images

Use Linux containers, a running Kubernetes cluster, kubectl, and Helm. SQL requests 2 GiB and has a 3 GiB limit; allow enough Docker Desktop memory for the remaining pods. These commands assume the kubeadm provisioner can use local images. For kind or another cluster, use registry images accessible to its nodes.

Commands run from the repository root. The image tags must match src/AspireApp1/k8s/apps.yaml; the current API tag is k8s-test-v2. Change the tag when rebuilding modified code so old images are not reused.

```powershell
kubectl config use-context docker-desktop
kubectl get nodes
docker build -f src/AspireApp1/AspireApp1.ApiService/Dockerfile -t patware/pipeline-api:k8s-test-v2 .
docker build -f src/AspireApp1/AspireApp1.Web/Dockerfile -t patware/pipeline-web:k8s-test .
helm repo add traefik https://traefik.github.io/charts
helm repo update
helm upgrade --install traefik traefik/traefik --namespace traefik --create-namespace
kubectl -n traefik rollout status deployment/traefik --timeout=180s
kubectl apply -f src/AspireApp1/k8s/dependencies.yaml
```

The chart installs Traefik's CRDs. See [Traefik services](https://doc.traefik.io/traefik/reference/routing-configuration/kubernetes/crd/http/service/) and [Docker Desktop Kubernetes](https://docs.docker.com/desktop/use-desktop/kubernetes/).

## SQL credentials

SQL Server requires at least eight password characters and three of uppercase, lowercase, digits, and symbols. Both Secret keys must use the same password. This creates or updates the Secret without committing credentials:

```powershell
$sqlCredential = Get-Credential -UserName sa -Message "Enter a strong SQL test password"
$sqlPassword = $sqlCredential.GetNetworkCredential().Password
$dbConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$dbConnection["Server"] = "sql,1433"
$dbConnection["Database"] = "Pipeline"
$dbConnection["User ID"] = "sa"
$dbConnection["Password"] = $sqlPassword
$dbConnection["Encrypt"] = "True"
$dbConnection["TrustServerCertificate"] = "True"
kubectl -n pipeline-test create secret generic pipeline-db `
    "--from-literal=sa-password=$sqlPassword" `
    "--from-literal=connection-string=$($dbConnection.ConnectionString)" `
    --dry-run=client -o yaml | kubectl apply -f -
Remove-Variable sqlPassword, sqlCredential, dbConnection
kubectl -n pipeline-test rollout status deployment/sql --timeout=300s
kubectl -n pipeline-test rollout status deployment/cache --timeout=180s
```

Existing pods must restart to reload Secret-backed environment variables. Restart SQL after correcting a rejected initial password and existing API pods after connection-string changes. MSSQL_SA_PASSWORD does not rotate the password of an already initialized database; use SQL password rotation for that case. See [SQL container setup](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker).

## Deploy

Files under src/AspireApp1/k8s define dependencies and volumes, application Deployments/Services, and Traefik routes. For first database initialization, temporarily set API replicas to 1 in apps.yaml, apply, wait for readiness, then restore replicas to 2 and reapply. EF Core 9 and later lock migration execution; schema upgrades still need deployment-specific validation.

```powershell
kubectl apply -f src/AspireApp1/k8s/apps.yaml
kubectl -n pipeline-test rollout status deployment/apiservice --timeout=300s
kubectl -n pipeline-test rollout status deployment/webfrontend --timeout=180s
kubectl apply -f src/AspireApp1/k8s/routes.yaml
kubectl -n pipeline-test get pods -o wide
```

In a separate terminal:

```powershell
kubectl -n traefik port-forward service/traefik 8088:80
```

Open http://localhost:8088/pipeline. Forwarding Traefik exercises its downstream balancing. Forwarding an application Service directly selects a pod and bypasses that path.

## Configuration

| Setting | Purpose |
| --- | --- |
| ConnectionStrings__Pipeline | Shared SQL database on every executor. |
| Pipeline__RequireSharedStorage | Reject accidental in-memory backend configuration. |
| Pipeline__RunLogFormattingDemoOnStartup=false | Avoid extra runs on pod restarts. |
| Pipeline__EnableDemoEndpoints=true | Enable sample submissions before app.Run. |
| ConnectionStrings__cache | Redis caching and shared Data Protection storage. |
| services__apiservice__http__0 | Logical service endpoint using Kubernetes DNS. |
| Hosting__ExposeHealthEndpoints | Enable /health and /alive outside Development. |
| Hosting__UseHttpsRedirection=false | Local HTTP verification. |

Backend routes /api/pipeline and /demo take precedence over the frontend catch-all. Only frontend routing uses a sticky cookie. API readiness includes SQL connectivity; liveness excludes dependency checks. Frontends use the same Data Protection application name and Redis key. Affinity keeps a circuit on its frontend; it does not transfer circuits.

## Shared reads and worker distribution

```powershell
$formattingRun = Invoke-RestMethod -Method Post -Uri http://localhost:8088/demo/log-formatting
$instances = foreach ($attempt in 1..20) {
    $response = Invoke-WebRequest -DisableKeepAlive `
        -Uri "http://localhost:8088/api/pipeline/runs/$($formattingRun.id)/execution"
    $execution = $response.Content | ConvertFrom-Json
    if ($execution.run.id -ne $formattingRun.id) { throw "Unexpected run identity." }
    $response.Headers["X-Instance-Id"]
}
$instances | Sort-Object -Unique
$probeRuns = foreach ($attempt in 1..4) {
    Invoke-RestMethod -Method Post -Uri http://localhost:8088/demo/probe
}
kubectl -n pipeline-test logs -l app=apiservice --all-containers=true --prefix=true --tail=100
```

Verify the same run is readable through both API pod names. Inspect probe logs for execution on both workers. Submission pod metadata proves submission routing, not execution ownership. Use independent browser sessions or requests to check both frontend X-Instance-Id values; an established browser session should remain attached to its selected pod.

## Recovery and persistence checks

Record a running probe's ID and executing pod from its persisted start log. Interrupt that pod and confirm its old container stopped. Verify the same run remains readable and ultimately completes, with a subsequent invocation and completion from the observed replacement owner.

The lease lasts two minutes and renews every twenty seconds. Recovery may wait for expiry and repeat invocation. The probe only logs and waits. Force-deleting a pod object alone does not establish that its old process stopped; claim checks and external idempotency remain necessary under partitions.

After completion, restart backends and confirm the same IDs, results, and logs remain:

```powershell
kubectl -n pipeline-test rollout restart deployment/apiservice
kubectl -n pipeline-test rollout status deployment/apiservice --timeout=300s
```

These checks do not establish exactly-once external effects, durable event delivery, seamless frontend circuit recovery, or SQL/Redis high availability. This local topology has single datastore instances.

## Troubleshooting

```powershell
kubectl -n pipeline-test get pods -o wide
kubectl -n pipeline-test describe pods -l app=sql
kubectl -n pipeline-test logs deployment/sql --tail=150
kubectl -n pipeline-test get events --sort-by=.metadata.creationTimestamp
kubectl -n pipeline-test get pvc
```

For CrashLoopBackOff, inspect current and previous container logs: password validation, storage permissions, and OOMKilled are different causes. Pending pods may lack memory or a bound PVC. ImagePullBackOff can indicate an unavailable tag or image-store mismatch.

For demo POST 404s, check routing, image tag, enablement configuration, and endpoint order. Every MapPost must precede app.Run. Rebuild with a new tag, update apps.yaml, and wait for rollout before retesting.
