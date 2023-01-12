# Log-replication
First iteration of the log replication task of Distributed Systems course.

## Configuration 
### To configure a new secondary:
 1. Define new secondary service in docker-compose.yml. Ex.
 ````
   secondary2:
     container_name: secondary_2
     image: ${DOCKER_REGISTRY-}secondary
     build:
       context: .
       dockerfile: Secondary/Dockerfile
  ````
  2. Define new service environemnt variables, ports, volumes, and delay on node write (WriteDelay in ms). Ex.
  ````
    secondary2:
      environment:
        - ASPNETCORE_ENVIRONMENT=Development
        - ASPNETCORE_URLS=https://+:443;http://+:80
        - WriteDelay=3000
      ports:
        - "51985:80"
        - "51984:443"
      volumes:
        - ${APPDATA}/Microsoft/UserSecrets:/root/.microsoft/usersecrets:ro
        - ${APPDATA}/ASP.NET/Https:/root/.aspnet/https:ro
   ````
  3. Go to Master appsettings.json and add new container_name to Secondaries array. Note container_name defined in 1. should match the one you add to Secondaries            array.
### New message POST request configuration: 
 1. To configure write concern on new message append request set ````writeConcern```` param to number of nodes to wait. Ex. ````writeConcern```` = 3 - wait master and any two secondary nodes.
 2. To configure broadcast delay on new message append request set ````broadcastDelay```` param to number of ms to wait before starting delivering message to master and all secondary nodes. This enable us to emulate happens-before reation btw to messages. Ex. send ````m1```` message with ````broadcastDelay = 10000````, checks the logs of master for the ````TOTAL ORDER OF MESSAGE 'm1' IS '0'.```` log, which defines that total order of ````m1```` message is ````0````. After that send new ````m2```` message with no broadcast delay ````broadcastDelay = 0````, observe the total order of ````m2```` message equals ````1````. Also, you can see from the logs that ````m2```` message is processed faster on nodes than ````m1```` message, but because ````m1```` happened before ````m2````, ````m2```` will not be delivred from any nodes on GET request until ````m1```` can be delivered. 
###
### Resilience Policy:
To make system resilient we use common resilient policy: retry + circuit breaker + timeout:
 1. Retry is exponential, which means we wait ````2^retryNumber```` between retries. Each retry is infinite, which means that eventually each message should be             delivered to all secondaries.
 2. Circuit breaker is automatically moved between states (open, clsoed) based on the heartbeats.
 3. Timeout of secondary service call can be configured with the help of ````SecondaryCallTimeout```` option in the appsettings.json of the master.
###
### Health Checks
 1. Health Check calls to secondaries are done every 3 seconds. Health Check call timeout is 2 seconds.
 2. Go to https://localhost:51978/hc-ui to check health status of secondaries:
  ![image](https://user-images.githubusercontent.com/25819135/212131313-588105ff-d738-4e99-a5aa-2fd9972c6462.png)
    or you can use https://localhost:51978/hc-api to get secondaries health in json format.
 3. Health checks to secondaries are done in the background service. Master is get notified on any secondary status change with the help of registered web hook, which     you can check on https://localhost:51978/hc-ui:
    ![image](https://user-images.githubusercontent.com/25819135/212138639-95d98e77-5d80-4e80-9925-10b15a7c7cfe.png)
    And, once the node is detected as unhealthy:
    - master opens circuit breaker for that secondary grpc client, so that any subsequent requests fail fast without making real calls to unhealthy secondary, thus           allowing the secondary to recover. After master detects that secondary is healthy again, the circuit breaker is closed and real calls can be made again to the         secondary.
    - and ````EnforceQuorumAppend```` option is enabled in the appsettings.json of the master, the master can be moved to readonly mode (no new messages can be added)       if number of unhealthy nodes is greater or equal than half of the nodes. Accordingly, when number of healthy nodes become greater than half of the nodes,               readonly mode is off on the master, and new massages can be added again.
## How to run
   * Run: docker-compose -f "docker-compose.yml" -f "docker-compose.override.yml" up
