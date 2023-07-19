FROM mcr.microsoft.com/dotnet/sdk:7.0 as builder

COPY . /src
WORKDIR "/src"  
RUN  dotnet restore "DynatraceHelloWorld.sln" &&  \
     dotnet build "DynatraceHelloWorld.sln" &&  \
     dotnet test "DynatraceHelloWorld.sln"  &&  \
     dotnet publish "DynatraceHelloWorld/DynatraceHelloWorld.csproj" -c Release -o /src/out
  
# Build runtime image  
FROM mcr.microsoft.com/dotnet/aspnet:7.0 as prod
ENV API_PORT=5000
WORKDIR /src  
COPY --from=builder /src/out .  
ENTRYPOINT ["dotnet", "DynatraceHelloWorld.dll"]