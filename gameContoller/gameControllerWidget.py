import sys
from PyQt5.QtWidgets import (QWidget, QGridLayout, QVBoxLayout, QPushButton, QApplication, QLabel, QComboBox, QLineEdit, QSizePolicy)
from PyQt5.QtGui import QIcon
from PyQt5 import uic
from PyQt5.QtCore import QTimer



class GameControllerWidget(QWidget):
    def __init__(self):
        super(GameControllerWidget, self).__init__()

        self.timer = QTimer()
        self.min = 0
        self.sec = 0

        self.step = 0

        self.createWidget()
        self.connections()

    def createWidget(self):
        uic.loadUi('gcWidget.ui', self) # Load the .ui file
        self.show()


    def connections(self):
        self.timer.timeout.connect(self.handleTimer)
    #     self.pbleft.pressed.connect(self.pbleft_pressed)
    #     self.pbright.pressed.connect(self.pbright_pressed)
    #     self.pbup.pressed.connect(self.pbup_pressed)
    #     self.pbdown.pressed.connect(self.pbdown_pressed)
    #     self.pbcw.pressed.connect(self.pbcw_pressed)
    #     self.pbccw.pressed.connect(self.pbccw_pressed)
    #
    #     self.pbleft.released.connect(self.pb_released)
    #     self.pbright.released.connect(self.pb_released)
    #     self.pbup.released.connect(self.pb_released)
    #     self.pbdown.released.connect(self.pb_released)
    #     self.pbcw.released.connect(self.pb_released)
    #     self.pbccw.released.connect(self.pb_released)
    #
    #     self.lineSpeed.textChanged.connect(self.speed_changed)

    def startTimer(self):
        self.timer.start(1000)

    def handleTimer(self):
        self.sec += 1
        if self.sec == 60:
            self.min += 1
            self.sec = 0
        self.labelTimer.setText(str(self.min) + ':' + str(self.sec))

    def updateStats(self):
        self.step += 1
        self.labelsteper.setText('step ' + str(self.step) + ' / 18000')


if __name__ == '__main__':
    app = QApplication(sys.argv)
    ex = GameControllerWidget()
    sys.exit(app.exec_())

