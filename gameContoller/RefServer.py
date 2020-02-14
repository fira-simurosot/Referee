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


class GameControllerServicer(service_pb2_grpc.GameControllerServicer):

    def handyReferee(self, request, context):
        print("handyReferee")
        response = messages_pb2.HandyRef()
        return response



app = QApplication(sys.argv)
myWidget = gameControllerWidget.GameControllerWidget()


min = 0
sec = 0
def countTime():
    global sec
    global min
    sec += 1
    if sec == 60:
        min += 1
        sec = 0
    myWidget.labelTimer.setText(str(min) + ':' + str(sec))


step = 0
timer = QTimer()
timer.timeout.connect(countTime)



def virtualGameControllerServicer():
    global step
    if step == 0:
        timer.start(1000)
    step += 1
    request = messages_pb2.Environment()
    # print(request.statistics.scoreYellow)
    myWidget.labelsteper.setText(str(step) + ' / 18000')

steper = QTimer()
steper.timeout.connect(virtualGameControllerServicer)
steper.start(16.6)


server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
service_pb2_grpc.add_GameControllerServicer_to_server(GameControllerServicer(),server)
server.add_insecure_port('127.0.0.1:50055')
server.start()
















sys.exit(app.exec_())

