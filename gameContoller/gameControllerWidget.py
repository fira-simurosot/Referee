import sys
from PyQt5.QtWidgets import (QWidget, QGridLayout, QVBoxLayout, QPushButton, QApplication, QLabel, QComboBox, QLineEdit, QSizePolicy)
from PyQt5.QtGui import QIcon
from PyQt5 import uic



class GameControllerWidget(QWidget):
    def __init__(self):
        super(GameControllerWidget, self).__init__()

        self.createWidget()
        self.connections()

    def createWidget(self):
        uic.loadUi('gcWidget.ui', self) # Load the .ui file
        self.show()


    def connections(self):
        pass
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





if __name__ == '__main__':
    app = QApplication(sys.argv)
    ex = GameControllerWidget()
    sys.exit(app.exec_())

