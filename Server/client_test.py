import socket
import threading
import SocketServer
import json


def message(sock, data):
    message = json.dumps(data, separators = (',',':'))
    with open("/hermes/logs/log", "a") as log:
        log.write("Sending: {}\n".format(message))
    sock.sendall(message + "\n")
    response = sock.makefile().readline()
    with open("/hermes/logs/log", "a") as log:
        log.write("Received: {}\n".format(response))
    return response

def query_message(sock, query):
    data = {'type': 'query', 'string' : query, 'limit' : 10, 'offset' : 0}
    return message(sock, data)

def heartbeat_start(sock, fileid, event):
    data = {'type': 'request', 'files': {fileid: event},'peerID' : 'yyy', 'port' : 4444, 'ip' : '198.161.1.1', 'maxPeers' : 2}
    threading.Thread(target = message, args = [sock, data]).start()

def test_client(ip, port):
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((ip, port))
    try:
        #query_message(sock, "SuperMan")
        #query_message(sock, "Batman")
        for i in range(100):
            heartbeat_start(sock, "1", "started")
        #heartbeat_start(sock, "1", "stopped")

        
    finally:
        sock.close()


if __name__ == "__main__":
    ip, port = "localhost", 9999
    test_client(ip, port)