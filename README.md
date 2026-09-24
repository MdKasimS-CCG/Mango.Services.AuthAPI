# Authentication API for Mango application.

# Running Docker via any terminal

## Building docker image
docker build --secret id=nuget_config,src="$env:NUGET_CONFIG_PATH" -t mango-authapi-local:dev .

## Running container
docker run --name mango-authapi  --env-file .env -p 5005:8080 mango-authapi-local:dev
