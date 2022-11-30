#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["JMS.Proxy/JMS.Proxy.csproj", "JMS.Proxy/"]
COPY ["JMS.Common/JMS.Common.csproj", "JMS.Common/"]
RUN dotnet restore "JMS.Proxy/JMS.Proxy.csproj"
COPY . .
WORKDIR "/src/JMS.Proxy"
RUN dotnet build "JMS.Proxy.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "JMS.Proxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JMS.Proxy.dll"]