# Game Controller

<p align="center">
<img src="resources/screenshot.png" alt="GameController"
title="GameController" width="200" align="middle" />
</p>

## Usage

Game Controller is a app designed to show game statistics and send owerwrite commands to referee.


## Installation

### Clone Sub Modules
```bash
git submodule update --recursive --init 
```

### creating virtualenv
#### Linux and macOS
```bash
pip3 install virtualenv
cd /path/to/gameContoller/directory
virtualenv env
source env/bin/activate
pip3 install -r requirements.txt
```

### run the app

#### Linux and macOS
```bash
python3 RefServer.py
```


