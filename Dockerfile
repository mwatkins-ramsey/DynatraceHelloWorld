FROM 058238361356.dkr.ecr.us-east-1.amazonaws.com/innersource/dotnet/rs-dotnet70:IR.1.0.0-builder-2023-06-30-T19.36.13 as build

COPY . /src
WORKDIR "/src"  
RUN  dotnet restore "DynatraceHelloWorld.sln" &&  \
     dotnet build "DynatraceHelloWorld.sln" -c Release --no-restore &&  \
     dotnet test "DynatraceHelloWorld.sln" -c Release --no-build &&  \
     dotnet publish "DynatraceHelloWorld/DynatraceHelloWorld.csproj" -c Release -o /src/out
  
# Build runtime image  
FROM 058238361356.dkr.ecr.us-east-1.amazonaws.com/innersource/dotnet/rs-dotnet70:IR.1.0.0-2023-06-30-T19.36.27 as prod
ENV API_PORT=5000
WORKDIR /src  
COPY --from=build /src/out .  
ENTRYPOINT ["dotnet", "DynatraceHelloWorld.dll"]