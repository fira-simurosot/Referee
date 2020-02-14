import subprocess
subprocess.call("./protoCompiler.bash", shell=True)
import sys
sys.path.insert(1, './messages')

import grpc
import time
from concurrent import futures
import gameControllerWidget
from REF2CLI import service_pb2_grpc
from REF2CLI import messages_pb2
import common_pb2
from PyQt5.QtWidgets import (QWidget, QGridLayout,QPushButton, QApplication, QLabel, QComboBox)
from PyQt5.QtCore import QTimer

##create gamecontroller GUI
app = QApplication(sys.argv)
myWidget = gameControllerWidget.GameControllerWidget()


##initiate Referee communication
class GameControllerServicer(service_pb2_grpc.GameControllerServicer):

    def handyReferee(self, request, context):
        print("handyReferee")
        if myWidget.step == 0:
            myWidget.startTimer()

        response = myWidget.updateStats(request.statistics.scoreBlue, request.statistics.scoreYellow)
        if response.ByteSize() != 0:
            print(response)
        return response

server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
service_pb2_grpc.add_GameControllerServicer_to_server(GameControllerServicer(),server)
server.add_insecure_port('127.0.0.1:50055')
server.start()



##testing the app without referee server
def virtualGameControllerServicer():
    if myWidget.step == 0:
        myWidget.startTimer()

    request = messages_pb2.Environment()
    response = myWidget.updateStats(request.statistics.scoreBlue, request.statistics.scoreYellow)
    if response.ByteSize() != 0:
        print(response)
    return response

steper = QTimer()
steper.timeout.connect(virtualGameControllerServicer)
steper.start(16.6)






sys.exit(app.exec_())

