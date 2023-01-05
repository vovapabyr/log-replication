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
## How to run
   * Run: docker-compose -f "docker-compose.yml" -f "docker-compose.override.yml" up
