#!/usr/bin/env bash
# Probe: real images under patched PRoot with --net-map (per-container loopback addresses).
P=$TABSTUDIO_CONTAINER_PROOT; export PROOT_LOADER=$TABSTUDIO_CONTAINER_LOADER PROOT_TMP_DIR=$TABSTUDIO_CONTAINER_TMP
arch=$(uname -m); [ "$arch" = aarch64 ] && a=arm64 || a=amd64
W=$RUNNER_TEMP/c; mkdir -p $W
for i in postgres:17-alpine redis:7-alpine nginx:alpine; do python3 build/probe/pull.py $i $W/${i%%:*} $a; done
pr() { r=$1; shift; "$P" --kill-on-exit --link2symlink --sysvipc -L -0 -r "$r" -b /dev -b /proc -b /sys -b /dev/urandom:/dev/random -b /proc/self/fd:/dev/fd -b "$r/tmp:/dev/shm" "$@"; }
s=$(date +%s.%N); cp -a $W/postgres $W/pg2; echo "copy seconds $(echo "$(date +%s.%N) - $s" | bc)"
printf '127.0.0.1 localhost\n127.42.0.2 db\n127.42.0.3 db2\n' | tee $W/postgres/etc/hosts > $W/pg2/etc/hosts
printf 'C127.42.0.2:5432=127.0.0.1:15432\nC127.42.0.3:5432=127.0.0.1:25432\n' > $W/peers
env0="/usr/bin/env -i PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin PGDATA=/var/lib/postgresql/data POSTGRES_PASSWORD=pw LANG=en_US.utf8"
start=$(date +%s.%N)
pr $W/postgres --net-map=A127.42.0.2,P5432=15432,@$W/peers -w / $env0 /usr/local/bin/docker-entrypoint.sh postgres > $W/pg.log 2>&1 &
pr $W/pg2 --net-map=A127.42.0.3,P5432=25432,@$W/peers -w / $env0 /usr/local/bin/docker-entrypoint.sh postgres > $W/pg2.log 2>&1 &
for i in $(seq 1 90); do [ $(grep -c 'ready to accept' $W/pg.log) -ge 2 ] && [ $(grep -c 'ready to accept' $W/pg2.log) -ge 2 ] && break; sleep 1; done
echo "two postgres ready after $(echo "$(date +%s.%N) - $start" | bc)"
tail -3 $W/pg.log $W/pg2.log; ss -ltnp | grep -E 'postgres' 
echo "== host to published"; PGPASSWORD=pw pr $W/postgres -w / /usr/bin/env PGPASSWORD=pw psql -h 127.0.0.1 -p 15432 -U postgres -tc 'select 1'
echo "== client container by name"; pr $W/postgres --net-map=A127.42.0.9,@$W/peers -w / /usr/bin/env PGPASSWORD=pw psql -h db -U postgres -tc "select 'db ok'"
pr $W/postgres --net-map=A127.42.0.9,@$W/peers -w / /usr/bin/env PGPASSWORD=pw psql -h db2 -U postgres -tc "select 'db2 ok'"
echo "== self localhost"; pr $W/postgres --net-map=A127.42.0.2,P5432=15432 -w / /usr/bin/env PGPASSWORD=pw psql -h localhost -U postgres -tc "select 'self ok'"
pr $W/redis --net-map=A127.42.0.4,P6379=16379 -w /data /usr/local/bin/docker-entrypoint.sh redis-server > $W/redis.log 2>&1 &
sleep 3; tail -5 $W/redis.log
echo "== redis"; pr $W/redis -w / redis-cli -p 16379 ping
pr $W/nginx --net-map=A127.42.0.5,P80=18080 -w / /docker-entrypoint.sh nginx -g 'daemon off;' > $W/nginx.log 2>&1 &
sleep 4; tail -3 $W/nginx.log; ss -ltnp | grep -E 'nginx|redis'
echo "== nginx"; curl -s http://127.0.0.1:18080/ | grep title
du -sh $W/*/
kill $(jobs -p)
