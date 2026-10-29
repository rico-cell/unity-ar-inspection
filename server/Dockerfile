FROM golang:1.22-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -ldflags="-s -w" -o /out/inspection-server ./cmd/server

FROM alpine:3.20
RUN adduser -D -u 10001 app && mkdir -p /data && chown app:app /data
USER app
ENV ADDR=:8787
ENV DB_PATH=/data/issues.db
EXPOSE 8787
COPY --from=build /out/inspection-server /usr/local/bin/inspection-server
ENTRYPOINT ["inspection-server"]
