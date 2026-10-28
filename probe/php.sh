set -x
apk add --no-cache php84 php84-phar php84-mbstring php84-tokenizer php84-ctype php84-dom php84-xml php84-xmlwriter php84-simplexml php84-iconv php84-openssl php84-curl php84-fileinfo php84-pdo_sqlite php84-session composer php84-pecl-xdebug nodejs python3 > /tmp/apk.log 2>&1 || tail -20 /tmp/apk.log
ls -l /usr/bin/php* ; php -v; php84 -v; composer --version; php -m | tr '\n' ' '; echo
apk info -R composer | head; cat /etc/php84/conf.d/*xdebug* 
wget -q https://github.com/phpactor/phpactor/releases/download/2026.10.07.0/phpactor.phar -O /usr/local/bin/phpactor && chmod +x /usr/local/bin/phpactor
wget -q https://open-vsx.org/api/xdebug/php-debug/1.40.2/file/xdebug.php-debug-1.40.2.vsix -O /tmp/pd.vsix && mkdir -p /opt/pd && cd /opt/pd && unzip -q /tmp/pd.vsix && ls extension extension/out | head -30
python3 /probe/php_tools.py
