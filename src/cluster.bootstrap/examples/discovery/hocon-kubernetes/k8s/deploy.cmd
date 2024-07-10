set LOCAL=%~dp0
kubectl apply -f "%~dp0/clusterbootstrap.yaml"
kubectl get all -n hocon-cluster-bootstrap