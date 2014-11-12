import socket
import threading
import SocketServer
import json
import time

hb_time = 0

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

def heartbeat(sock, fileid, event):
    global hb_time
    time.sleep(hb_time/1000)
    data = {'type': 'heartbeat', 'files': {fileid: event},'peerID' : 'yyy', 'port' : 4444, 'ip' : '198.161.1.1', 'maxPeers' : 2}
    response = message(sock, data)
    response_data = json.loads(response)
    hb_time = response_data['interval']
    return response

def upload(sock, fileName, size, pieceSize, blockSize, piecesSHA1s):
    data = {'type': 'upload',
            'fileName': fileName,
            'size': size, 
            'pieceSize': pieceSize,
            'blockSize': blockSize, 
            'piecesSHA1S': piecesSHA1s, 
            'peerID' : 'yyy', 
            'port' : 4444, 
            'ip' : '198.161.1.1'}
    return message(sock, data)

def test_client(ip, port):
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((ip, port))
    try:
        #[heartbeat(sock, "1", "active") for i in range(10)]
        #heartbeat(sock, "1", "inactive")
        upload(sock, 'Captain America', 2000000, 10000, 10, [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12]])
        upload(sock, 'Captain America 2', 3000000, 10000, 10, [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 13]])
        upload(sock, 'The Avengers', 4000000, 10000, 10, [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 14]])
        upload(sock, 'The Notebook', 5000000, 10000, 10, [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 15]])
        upload(sock, 'Captured', 6000000, 10000, 10, [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 15]])
        query_message(sock, "Cap")
        query_message(sock, "Cap ame")
        query_message(sock, "Capt america 2")
        query_message(sock, "the")
        query_message(sock, "the captain")
        query_message(sock, "the aveng")
        
    finally:
        sock.close()


if __name__ == "__main__":
    ip, port = "localhost", 9999
    test_client(ip, port)