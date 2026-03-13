\## 実行方法 (Usage)



本システムを起動するには、用途に合わせて以下のいずれかの手順でコマンドを実行してください。

※各コマンドは、それぞれ新しいターミナルを開いて実行してください。



\### A. シミュレーションのみで実行する場合 (SIM ONLY)

UnityのAR環境とROS2のシミュレーションのみで軌道制御を行う場合の手順





\*\*1. MoveIt2! 環境の起動\*\*

```bash

ros2 launch utra6\\\_550**\\\_**moveit\\\_config demo.launch.py

```


\*\*2. Unity通信用TCPエンドポイントの起動\*\*

```bash
ros2 run ros\\\_tcp\\\_endpoint default\\\_server\\\_endpoint --ros-args -p tcp\\\_ip:=0.0.0.0 -p tcp\\\_port:=10000 \\\&

```



\*\*3. 制御用メインスクリプトの起動\*\*

```bash
ros2 run utra6\\\_moveit mover.py
```



\### B. 実機と連携して実行する場合 (SIM \& REAL)

実機のロボットアームを接続し、AR環境と同期させて制御する場合の手順



\*\*1. 実機通信用APIサーバーの起動\*\*

```bash
ros2 launch arm\_controller utarm\_api\_server.launch.py
```



\*\*2. MoveIt2! 環境の起動\*\*

```bash
ros2 launch utra6\\\_550\\\_moveit\\\_config demo.launch.py
```



\*\*3. Unity通信用TCPエンドポイントの起動\*\*

```bash
ros2 run ros\\\_tcp\\\_endpoint default\\\_server\\\_endpoint --ros-args -p tcp\\\_ip:=0.0.0.0 -p tcp\\\_port:=10000 \\\&
```


\*\*4. 制御用メインスクリプトの起動\*\*

```bash
ros2 run utra6\\\_moveit mover.py
```



