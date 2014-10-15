#! /bin/sh -e

echo -n "Creating folders... "
u=$USER
sudo rm -rf /hermes/
sudo mkdir /hermes/
sudo chown $u:$u /hermes/

SERVER=/hermes/server
LOGS=/hermes/logs
mkdir $SERVER
mkdir $LOGS
echo "OK"

echo -n "Copying scripts... "
cp hermes.py $SERVER/hermes.py
sudo chmod +x $SERVER/hermes.py
cp hermes.init $SERVER/hermes.init
sudo chmod +x $SERVER/hermes.init
echo "OK"

echo -n "Setup up daemon... "
sudo rm -f /etc/init.d/hermes
sudo ln -s $SERVER/hermes.init /etc/init.d/hermes
sudo update-rc.d hermes defaults > /dev/null
echo "OK"

