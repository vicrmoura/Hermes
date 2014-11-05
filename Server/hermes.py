#! /usr/bin/python

import socket
import threading
import time
import SocketServer
import json
import random

MAX_SEARCH_LIMIT = 1000
MAX_PEERS_IN_RESPONSE = 100
HEARTBEAT_INTERVAL = 1000

file_info = {}
search_map = {}

def query_test():
    global file_info
    global search_map
    
    file_info = {"1" : { 
                    "name": "Batman Begins",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {"aaa":{"timestamp": 10, "port": 3000, "ip": "161.24.24.1"}, "bbb":{"timestamp": 500, "port": 6666, "ip": "11.12.13.14"}, "ccc":{"timestamp": 800, "port": 1111, "ip": "10.1.1.2"}}}, 
             "2" : {
                    "name": "Batman: The Dark Knight",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {"aaa":{"timestamp": 50, "port": 3000, "ip": "161.24.24.1"}, "bbb":{"timestamp": 75, "port": 6666, "ip": "11.12.13.14"}}},
             "3" : {
                    "name": "Batman: The Dark Knight Rises",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {"bbb":{"timestamp": 59, "port": 6666, "ip": "11.12.13.14"}}},
             "4" : {
                    "name": "Superman",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [],
                    "peers": {}}
            }

    search_map = {"batman" : ["1", "2", "3"], "superman" : ["4"]}

def current_time_ms():
    return int(time.time()*1000.0)

def convert_data_jsonline(data):
    return json.dumps(data, separators = (',',':'))+"\n"

def log(log_message):
    with open("/hermes/logs/log", "a") as log:
        log.write("Server on thread {}: {}\n".format(threading.current_thread().name, log_message))

def log_missing_field(field, data):
    log("Missing field: {} from: {}".format(field, data))

def log_bad_json(bad_json):
    log("Bad json: {}".format(bad_json))

def search(search_text, limit, offset):
    match_ids = search_map[search_text.lower()][offset:min(MAX_SEARCH_LIMIT, limit)]
    results = [ {
                    "name": file_info[i]["name"],
                    "size": file_info[i]["size"],
                    "fileID": i,
                    "numOfPeers": len(file_info[i]["peers"])
                } for i in match_ids ]
    return {"type" : "queryresponse", "results": results}

def sample_peers(fileID, peerID, maxPeers):
    if peerID in file_info[fileID]["peers"]:
        peer_entry = file_info[fileID]["peers"][peerID]
        del file_info[fileID]["peers"][peerID]
    else: 
        peer_entry = None

    numOfPeers = min(maxPeers, MAX_PEERS_IN_RESPONSE, len(file_info[fileID]["peers"]))

    # Reservoir sampling
    iterator = iter(file_info[fileID]["peers"])    
    result = [next(iterator) for _ in range(numOfPeers)]

    n = numOfPeers
    for item in iterator:
        n += 1
        s = random.randint(0, n)
        if s < numOfPeers:
            result[s] = item

    if peer_entry != None: 
        file_info[fileID]["peers"][peerID] = peer_entry

    return [{"peerID" : peerid, 
             "port" : file_info[fileID]["peers"][peerid]["port"],
             "ip" : file_info[fileID]["peers"][peerid]["ip"]} 
            for peerid in result]
    

def process_heartbeat_started(fileID, peerID, port, ip):
    process_heartbeat_downloading(fileID, peerID, port, ip)
    log(peerID + " started on " + fileID)

def process_heartbeat_stopped(fileID, peerID, port, ip):
    if peerID in file_info[fileID]["peers"]:
        del file_info[fileID]["peers"][peerID]

def process_heartbeat_downloading(fileID, peerID, port, ip):
    file_info[fileID]["peers"][peerID] = {"timestamp": current_time_ms(), "port": port, "ip": ip}

def process_heartbeat_completed(fileID, peerID, port, ip):
    file_info[fileID]["peers"][peerID] = {"timestamp": current_time_ms(), "port": port, "ip": ip}

def process_heartbeat(files, peerID, port, ip, maxPeers):
    response = { "type": "response", "peers": {}, "interval": HEARTBEAT_INTERVAL}
    for fileID, event in files.iteritems():
        if event == "started":
            process_heartbeat_started(fileID, peerID, port, ip)
        elif event == "stopped":
            process_heartbeat_stopped(fileID, peerID, port, ip)
        elif event == "completed":
            process_heartbeat_completed(fileID, peerID, port, ip)
        elif event == "downloading":
            process_heartbeat_stopped(fileID, peerID, port, ip)
        response["peers"][fileID] = sample_peers(fileID, peerID, maxPeers)
    return response

class ThreadedTCPRequestHandler(SocketServer.StreamRequestHandler):

    def handle_search_query(self):
        if "string" not in self.data:
            log_missing_field("string", self.data)
            return ""
        search_text = self.data["string"]

        limit = MAX_SEARCH_LIMIT
        if "limit" in self.data:
            limit = self.data["limit"]
            if limit <= 0:
                log ("Search limit should be greater than zero," + 
                        " but is: {} from: {}".format(limit, self.data))
                return ""
        
        offset = 0
        if "offset" in self.data:
            offset = self.data["offset"]
            if offset < 0:
                log ("Search offset should not be negative," + 
                        " but is: {} from: {}".format(offset, self.data))
                return ""
        
        result = search(search_text, limit, offset)
        return convert_data_jsonline(result)

    def handle_heartbeat(self):
        if "files" not in self.data:
            log_missing_field("files", self.data)
            return ""
        files = self.data["files"]

        if "peerID" not in self.data:
            log_missing_field("peerID", self.data)
            return ""
        peerID = self.data["peerID"]

        if "port" not in self.data:
            log_missing_field("port", self.data)
            return ""
        port = self.data["port"]

        if "ip" not in self.data:
            log_missing_field("ip", self.data)
            return ""
        ip = self.data["ip"]

        maxPeers = MAX_PEERS_IN_RESPONSE
        if "maxPeers" in self.data:
            maxPeers = self.data["maxPeers"]
            if maxPeers < 0:
                log ("maxPeers should not be negative," + 
                        " but is: {} from: {}".format(maxPeers, self.data))
                return ""

        result = process_heartbeat(files, peerID, port, ip, maxPeers)
        if result == "":
            return ""
        return convert_data_jsonline(result)

    def handle(self):
        
        while True:
            message = self.rfile.readline()
            if message == "":
                break
            message = message.strip()
            log ("Message Received: {}".format(message))
            try:
                self.data = json.loads(message)
            except ValueError, e:
                log_bad_json("type")
                continue

            if "type" not in self.data:
                log_missing_field("type", self.data)
                continue

            if self.data["type"] == "query":
                response = self.handle_search_query()
            elif self.data["type"] == "request":
                response = self.handle_heartbeat()
            else:
                log("Unable to match type: {} from: {}".format(self.data["type"], self.data))
                continue

            if response != "":
                self.request.sendall(response)
                log("Response Sent: {}".format(response))

def check_heartbeat(wait_interval_ms):
    
    while True:
        time.sleep(wait_interval_ms/1000)
        current_time = current_time_ms()
        for fileID, info in file_info.iteritems():
            for peerID in info['peers'].keys():
                if current_time - info['peers'][peerID]['timestamp'] > wait_interval_ms:
                    del info['peers'][peerID]
                    log("Peer " + peerID + " removed from " + fileID)


class ThreadedTCPServer(SocketServer.ThreadingMixIn, SocketServer.TCPServer):
    pass


if __name__ == "__main__":
    query_test()

    HOST, PORT = "localhost", 9999

    server = ThreadedTCPServer((HOST, PORT), ThreadedTCPRequestHandler)
    ip, port = server.server_address

    server_thread = threading.Thread(target=server.serve_forever)
    server_thread.daemon = False
    server_thread.start()

    heartbeat_thread = threading.Thread(target=check_heartbeat, args = [2*HEARTBEAT_INTERVAL])
    heartbeat_thread.start()

    log("Started")
