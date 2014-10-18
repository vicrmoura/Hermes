#! /usr/bin/python

import socket
import threading
import SocketServer
import json

MAX_SEARCH_LIMIT = 1000

file_info = {}
search_map = {}

def query_test():
    global file_info
    global search_map
    
    file_info = {1 : { 
                    "name": "Batman Begins",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {123:[], 11111:[]}}, 
             2 : {
                    "name": "Batman: The Dark Knight",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {123:[], 11111:[]}},
             3 : {
                    "name": "Batman: The Dark Knight Rises",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [], 
                    "peers": {11111:[]}},
             4 : {
                    "name": "Superman",
                    "size": 10000,
                    "piece_size": 100,
                    "block_size": 5,
                    "sha1s": [],
                    "peers": {}}
            }

    search_map = {"batman" : [1, 2, 3], "superman" : [4]}

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


class ThreadedTCPRequestHandler(SocketServer.StreamRequestHandler):

    def handle_query(self, data):
        if "string" not in data:
            log_missing_field("string", data)
            return
        search_text = data["string"]

        limit = MAX_SEARCH_LIMIT
        if "limit" in data:
            limit = data["limit"]
            if limit <= 0:
                log ("Search limit shold be greater than zero," + 
                        " but is: {} from: {}".format(limit, data))
                return
        
        offset = 0
        if "offset" in data:
            offset = data["offset"]
            if offset < 0:
                log ("Search offset shold not be negative," + 
                        " but is: {} from: {}".format(offset, data))
                return
        
        result = search(search_text, limit, offset)
        return convert_data_jsonline(result)

    def handle(self):
        
        while True:
            message = self.rfile.readline()
            if message == "":
                break
            message = message.strip()
            log ("Message Received: {}".format(message))
            try:
                data = json.loads(message)
            except ValueError, e:
                log_bad_json("type")
                continue

            if "type" not in data:
                log_missing_field("type", data)
                continue


            if data["type"] == "query":
                response = self.handle_query(data)
            else:
                log("Unable to match type: {} from: {}".format(data["type"], data))
                continue

            self.request.sendall(response)

            log("Response Sent: {}".format(response))


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
    log("Started")
