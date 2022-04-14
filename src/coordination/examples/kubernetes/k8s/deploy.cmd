set LOCAL=%~dp0
kubectl apply -f "%~dp0/kubernetes.stresstest.yaml"
kubectl get all -n stress-test