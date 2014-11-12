#! /usr/bin/python

import socket
import threading
import time
import SocketServer
import json
import random

#### Global Constants ####

MAX_SEARCH_LIMIT = 1000
MAX_PEERS_IN_RESPONSE = 100
HEARTBEAT_INTERVAL = 1000

#### Data Structures ####

file_info = {}
file_info_locks = {}
search_map = {}
search_map_add_lock = threading.RLock()
file_id_counter = 0
file_id_counter_lock = threading.RLock()

#### Logging Functions ####

def log(log_message):
    with open("/hermes/logs/log", "a") as log:
        log.write("Server on thread {}: {}\n".format(threading.current_thread().name, log_message))

def log_missing_field(field, data):
    log("Missing field: {} from: {}".format(field, data))

def log_bad_json(bad_json):
    log("Bad json: {}".format(bad_json))

#### Helper Functions ####

def test_init():
    global file_info
    global search_map
    file_info = {
         "1" : { 
                "name": "Batman Begins",
                "size" : 100000,
                "pieceSize": 100,
                "blockSize": 5,
                "sha1s": [], 
                "peers": {"aaa":{"timestamp": 10, "port": 3000, "ip": "161.24.24.1"}, "bbb":{"timestamp": 500, "port": 6666, "ip": "11.12.13.14"}, "ccc":{"timestamp": 800, "port": 1111, "ip": "10.1.1.2"}}}, 
         "2" : {
                "name": "Batman: The Dark Knight",
                "size" : 100000,
                "pieceSize": 100,
                "blockSize": 5,
                "sha1s": [], 
                "peers": {"aaa":{"timestamp": 50, "port": 3000, "ip": "161.24.24.1"}, "bbb":{"timestamp": 75, "port": 6666, "ip": "11.12.13.14"}}},
         "3" : {
                "name": "Batman: The Dark Knight Rises",
                "size" : 100000,
                "pieceSize": 100,
                "blockSize": 5,
                "sha1s": [], 
                "peers": {"bbb":{"timestamp": 59, "port": 6666, "ip": "11.12.13.14"}}},
         "4" : {
                "name": "Superman",
                "size" : 100000,
                "pieceSize": 100,
                "blockSize": 5,
                "sha1s": [],
                "peers": {}}
        }
    for fileID in file_info:
        file_info_locks[fileID] = threading.RLock()

    search_map = {"batman" : {"1", "2", "3"}, "superman" : {"4"}}

def current_time_ms():
    return int(time.time()*1000.0)

def convert_data_jsonline(data):
    return json.dumps(data, separators = (',',':'))+"\n"

def check_heartbeat(wait_interval_ms):
    while True:
        time.sleep(wait_interval_ms/1000)
        current_time = current_time_ms()
        for fileID in file_info.keys():
            with file_info_locks[fileID]:
                info = file_info[fileID]
                for peerID in info['peers'].keys():
                    if current_time - info['peers'][peerID]['timestamp'] > wait_interval_ms:
                        del info['peers'][peerID]
                        log("Peer " + peerID + " removed from " + fileID)

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

def find_by_sha1s(size, pieceSize, blockSize, piecesSHA1S):
    for fileID in file_info.keys():
        with file_info_locks[fileID]:
            if size != file_info[fileID]["size"]:
                continue
            if pieceSize != file_info[fileID]["pieceSize"]:
                continue
            if blockSize != file_info[fileID]["blockSize"]:
                continue
            filesha1s = file_info[fileID]["sha1s"]
            if len(filesha1s) != len(piecesSHA1S):
                continue
            similar = True
            for i in range(len(piecesSHA1S)):
                if len(piecesSHA1S[i]) != 4:
                    log("piece SHA1 with wrong size")
                    similar = False
                    break
                for j in range(4):
                    if piecesSHA1S[i][j] != filesha1s[i][j]:
                        similar = False
                        break
                if not similar:
                    break
            if similar:
                return fileID
    return None

def generate_new_id():
    with file_id_counter_lock:
        global file_id_counter
        file_id_counter += 1
        return str(file_id_counter)

def search_entries(fileName):
    names = fileName.lower().split()
    entries = []
    for name in names:
        if len(name) < 3:
            entries.append(name)
        else:
            for i in range(3, len(name)+1):
                entries.append(name[:i])
    return entries

def update_search_map(fileName, fileID):
    entries = search_entries(fileName)
    with search_map_add_lock:
        for entry in entries:
            if entry in search_map:
                search_map[entry].add(fileID)
            else:
                search_map[entry] = {fileID}

#### Communication Processing Functions ####

def process_search_query(search_text, limit, offset):
    names = search_text.lower().split()
    results = []
    matches = [search_map[name] for name in names if name in search_map]
    if len(matches) > 0:
        match_ids = list(set.intersection(*matches))[offset:min(MAX_SEARCH_LIMIT, limit)]
        for i in match_ids:
            with file_info_locks[i]:
                results.append( {
                        "name": file_info[i]["name"],
                        "size": file_info[i]["size"],
                        "fileID": i,
                        "numOfPeers": len(file_info[i]["peers"])
                    } )
    return {"type" : "queryResponse", "results": results}

def process_heartbeat_active(fileID, peerID, port, ip):
    file_info[fileID]["peers"][peerID] = {"timestamp": current_time_ms(), "port": port, "ip": ip}

def process_heartbeat_inactive(fileID, peerID, port, ip):
    if peerID in file_info[fileID]["peers"]:
        del file_info[fileID]["peers"][peerID]

def process_heartbeat(files, peerID, port, ip, maxPeers):
    response = { "type": "heartbeatResponse", "peers": {}, "interval": HEARTBEAT_INTERVAL}
    for fileID, event in files.iteritems():
        with file_info_locks[fileID]:
            if event == "active":
                process_heartbeat_active(fileID, peerID, port, ip)
            elif event == "inactive":
                process_heartbeat_inactive(fileID, peerID, port, ip)
            response["peers"][fileID] = sample_peers(fileID, peerID, maxPeers)
    return response

def process_upload(fileName, size, pieceSize, blockSize, piecesSHA1S, peerID, port, ip):
    fileID = find_by_sha1s(size, pieceSize, blockSize, piecesSHA1S)
    if fileID is None:
        fileID = generate_new_id()
        file_info_locks[fileID] = threading.RLock()
        file_info[fileID] = {"name": fileName,
                             "size": size,
                             "pieceSize": pieceSize,
                             "blockSize": blockSize,
                             "sha1s": piecesSHA1S,
                             "peers": {peerID: {"timestamp": current_time_ms(), "port": port, "ip": ip}}}
        update_search_map(fileName, fileID)
    return { "type": "uploadResponse", "fileID": fileID, "interval": HEARTBEAT_INTERVAL}

#### Protocol Handlers ####

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
        
        result = process_search_query(search_text, limit, offset)
        return convert_data_jsonline(result)

    def handle_upload(self):
        if "fileName" not in self.data:
            log_missing_field("fileName", self.data)
            return ""
        fileName = self.data["fileName"]

        if "size" not in self.data:
            log_missing_field("size", self.data)
            return ""
        size = self.data["size"]

        if "pieceSize" not in self.data:
            log_missing_field("pieceSize", self.data)
            return ""
        pieceSize = self.data["pieceSize"]

        if "blockSize" not in self.data:
            log_missing_field("blockSize", self.data)
            return ""
        blockSize = self.data["blockSize"]

        if "piecesSHA1S" not in self.data:
            log_missing_field("piecesSHA1S", self.data)
            return ""
        piecesSHA1S = self.data["piecesSHA1S"]

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

        result = process_upload(fileName, size, pieceSize, blockSize, piecesSHA1S, peerID, port, ip)
        if result == "":
            return ""
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
            elif self.data["type"] == "heartbeat":
                response = self.handle_heartbeat()
            elif self.data["type"] == "upload":
                response = self.handle_upload()
            else:
                log("Unable to match type: {} from: {}".format(self.data["type"], self.data))
                continue

            if response != "":
                self.request.sendall(response)
                log("Response Sent: {}".format(response))

class ThreadedTCPServer(SocketServer.ThreadingMixIn, SocketServer.TCPServer):
    pass


if __name__ == "__main__":
    HOST, PORT = "localhost", 9999
    server = ThreadedTCPServer((HOST, PORT), ThreadedTCPRequestHandler)
    ip, port = server.server_address

    server_thread = threading.Thread(target=server.serve_forever)
    server_thread.daemon = False
    server_thread.start()

    heartbeat_thread = threading.Thread(target=check_heartbeat, args = [2*HEARTBEAT_INTERVAL])
    heartbeat_thread.start()

    log("Started")
