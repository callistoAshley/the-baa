import http.client as client
import sys
import os

if len(sys.argv) < 3:
    print("3 arguments expected; found " + str(len(sys.argv)))
    exit(-1)
release_url = sys.argv[0] # the url to download the update from
calling_guild = sys.argv[1] # the id of the guild where the update command was called
calling_channel = sys.argv[2] # the id of the channel where the update command was called

connection = client.HTTPConnection("https://github.com/")
connection.request("GET", release_url.replace("https://github.com/", "/"))
response = connection.getresponse()