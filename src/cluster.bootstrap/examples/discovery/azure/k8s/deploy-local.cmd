kubectl apply -f "%~dp0/namespace.yaml"
kubectl apply -f "%~dp0/akka-service.yaml"
kubectl get all -n clusterbootstrap