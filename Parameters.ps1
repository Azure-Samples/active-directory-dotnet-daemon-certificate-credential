   # Variables for the registration of the AAD application for the Web API Service
    $serviceAadAppName = "TodoListService"
    $serviceHomePage = "https://localhost:44321"
    $serviceAppIdURI = "https://$tenantName/$serviceAadAppName"

    # Variables for the registration of the AAD application for the Daemon app
    $daemonAadAppName = "TodoListDaemonWithCert"
    $daemonHomePage = "https://$daemonAadAppName"
    $daemonAppIdURI = "https://$daemonAadAppName"
    $certificateName = "CN=TodoListDaemonWithCert"
