set -x
apk add --no-cache php85 php85-phar php85-mbstring php85-tokenizer php85-ctype php85-dom php85-xml php85-xmlwriter php85-simplexml php85-iconv php85-openssl php85-curl php85-fileinfo php85-pdo_sqlite php85-session php85-posix php85-tokenizer composer php85-pecl-xdebug nodejs python3 > /tmp/apk.log 2>&1 || tail -20 /tmp/apk.log
ls -l /usr/bin/php* ; php -v; php85 -v; composer --version; php -m | tr '\n' ' '; echo
apk info -R composer | head; cat /etc/php85/conf.d/*xdebug* 
wget -q https://github.com/phpactor/phpactor/releases/download/2026.10.07.0/phpactor.phar -O /usr/local/bin/phpactor && chmod +x /usr/local/bin/phpactor
wget -q https://open-vsx.org/api/xdebug/php-debug/1.40.2/file/xdebug.php-debug-1.40.2.vsix -O /tmp/pd.vsix && mkdir -p /opt/pd && cd /opt/pd && unzip -q /tmp/pd.vsix && ls extension extension/out | head -30
python3 /probe/php_tools.py
