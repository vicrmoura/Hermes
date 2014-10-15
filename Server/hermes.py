#! /usr/bin/python

from time import sleep

i = 0
with open("/hermes/logs/log", "a") as file:
    file.write("====\n")
    while True:
        sleep(1)
        file.write(str(i) + "\n")
        file.flush()
        i += 1
