# Log-replication
First iteration of the log replication task of Distributed Systems course.

## Configuration 
To configure a new secondary:
 1. Define new secondary service in docker-compose.yml. Ex
 ````
   secondary2:
     container_name: secondary_2
     image: ${DOCKER_REGISTRY-}secondary
     build:
       context: .
       dockerfile: Secondary/Dockerfile
  ````
  2. Define new service environemnt variables, ports, and volumes. Ex
  ````
    secondary2:
      environment:
        - ASPNETCORE_ENVIRONMENT=Development
        - ASPNETCORE_URLS=https://+:443;http://+:80
      ports:
        - "51985:80"
        - "51984:443"
      volumes:
        - ${APPDATA}/Microsoft/UserSecrets:/root/.microsoft/usersecrets:ro
        - ${APPDATA}/ASP.NET/Https:/root/.aspnet/https:ro
   ````
  3. Go to Master appsettings.json and add new container_name to Secondaries array. Note container_name defined in 1. should match the one you add to Secondaries            array.

## How to run
   * Build: docker-compose -f "docker-compose.yml" -f "docker-compose.override.yml" build
   * Run: docker-compose -f "docker-compose.yml" -f "docker-compose.override.yml" up
