set -x
dotnet --info | head -30
G=$(find /usr/share/dotnet /usr/lib/dotnet $DOTNET_ROOT -name PortableRuntimeIdentifierGraph.json 2>/dev/null | head -1)
echo "GRAPH=$G"
mkdir -p ci-out
cp "$G" ci-out/PortableRuntimeIdentifierGraph.json || true
dl() { curl -sSL "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" -o /tmp/$1-$2.nupkg && unzip -Z1 /tmp/$1-$2.nupkg > ci-out/layout-$1-$2.txt; }
dl microsoft.data.sqlclient 5.1.6
dl microsoft.data.sqlclient 5.2.2
dl microsoft.data.sqlclient 6.0.2
dl microsoft.data.sqlclient 7.0.3
dl system.data.sqlclient 4.8.6
dl microsoft.data.sqlclient.sni.runtime 5.2.0
dl sqlitepclraw.lib.e_sqlite3 2.1.11
dl sqlitepclraw.bundle_e_sqlite3 2.1.11
dl skiasharp.nativeassets.linux 3.119.0
dl skiasharp.nativeassets.android 3.119.0
dl microsoft.netcore.platforms 5.0.0
dl microsoft.data.sqlite 10.0.0
dl microsoft.data.sqlite.core 10.0.0
curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite.core/10.0.0/microsoft.data.sqlite.core.nuspec | grep -i depend | head -20
curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite/10.0.0/microsoft.data.sqlite.nuspec | grep -i depend | head -20
curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlclient/7.0.3/microsoft.data.sqlclient.nuspec | grep -i "group\|depend" | head -40
curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.sqlserver/8.0.0/microsoft.entityframeworkcore.sqlserver.nuspec | grep -i depend | head
unzip -o -q /tmp/microsoft.netcore.platforms-5.0.0.nupkg runtime.json -d /tmp/plat && cp /tmp/plat/runtime.json ci-out/platforms-5.0.0-runtime.json
unzip -Z1 /tmp/microsoft.data.sqlclient-5.1.6.nupkg | grep -i runtime.json
ls ci-out
wc -l ci-out/*
