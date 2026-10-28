#!/usr/bin/env bash
# Probe: real images under patched PRoot with --net-map.
set -x
P=$TABSTUDIO_CONTAINER_PROOT; export PROOT_LOADER=$TABSTUDIO_CONTAINER_LOADER PROOT_TMP_DIR=$TABSTUDIO_CONTAINER_TMP
arch=$(uname -m); [ "$arch" = aarch64 ] && a=arm64 || a=amd64
W=$RUNNER_TEMP/c; mkdir -p $W
for i in postgres:17-alpine redis:7-alpine nginx:alpine; do python3 build/probe/pull.py $i $W/${i%%:*} $a; done
pr() { r=$1; shift; "$P" --kill-on-exit --link2symlink --sysvipc -L -0 -r "$r" -b /dev -b /proc -b /sys -b /dev/urandom:/dev/random -b /proc/self/fd:/dev/fd -b "$r/tmp:/dev/shm" "$@"; }
cat $W/postgres.json
echo "127.0.0.1 localhost" > $W/postgres/etc/hosts
start=$(date +%s.%N)
pr $W/postgres --net-map=B5432=15432,C127.0.0.1:5432=127.0.0.1:15432 -w / /usr/bin/env -i PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin PGDATA=/var/lib/postgresql/data POSTGRES_PASSWORD=pw LANG=en_US.utf8 /usr/local/bin/docker-entrypoint.sh postgres > $W/pg.log 2>&1 &
for i in $(seq 1 90); do grep -q 'ready to accept connections' $W/pg.log && [ $(grep -c 'ready to accept' $W/pg.log) -ge 2 ] && break; sleep 1; done
echo "postgres ready after $(echo "$(date +%s.%N) - $start" | bc)"
tail -30 $W/pg.log
ss -ltnp | grep -E '15432|5432'
pr $W/postgres -w / /usr/bin/env PGPASSWORD=pw psql -h 127.0.0.1 -p 15432 -U postgres -c 'select version()'
pr $W/postgres --net-map=C127.0.0.1:5432=127.0.0.1:15432 -w / /usr/bin/env PGPASSWORD=pw psql -h 127.0.0.1 -p 5432 -U postgres -c 'select 42 as mapped'
pr $W/redis --net-map=B6379=16379 -w /data /usr/local/bin/docker-entrypoint.sh redis-server > $W/redis.log 2>&1 &
sleep 3; tail -5 $W/redis.log
pr $W/redis -w / redis-cli -p 16379 ping
pr $W/nginx --net-map=B80=18080 -w / /docker-entrypoint.sh nginx -g 'daemon off;' > $W/nginx.log 2>&1 &
sleep 4; tail -20 $W/nginx.log
curl -s http://127.0.0.1:18080/ | head -5
du -sh $W/*/ 
kill %1 %2 %3
