FROM microsoft/dotnet:2.0-sdk as builder
ADD . /src
WORKDIR /src
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.0-runtime
WORKDIR /opt/Flick2Influx/
COPY --from=builder /src/out /opt/Flick2Influx/

ENTRYPOINT ["dotnet", "/opt/Flick2Influx/Flick2Influx.dll"]