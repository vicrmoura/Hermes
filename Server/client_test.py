import socket
import threading
import SocketServer

def client(ip, port, message):
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((ip, port))
    try:
        sock.sendall(message)
        response = sock.recv(1024)
        with open("/hermes/logs/log", "a") as log:
    		log.write("Received: {}\n".format(response))
    finally:
        sock.close()


if __name__ == "__main__":
    ip, port = "localhost", 9999
    client(ip, port, "Hello World 1")
    client(ip, port, "Hello World 2")
    client(ip, port, "Hello World 3")