import os
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node
from ament_index_python.packages import get_package_share_directory

def generate_launch_description():
    # 引数の宣言
    tcp_ip = LaunchConfiguration('tcp_ip')
    tcp_port = LaunchConfiguration('tcp_port')

    tcp_ip_arg = DeclareLaunchArgument(
        'tcp_ip',
        default_value='0.0.0.0',
        description='TCP IP address for Unity connection'
    )

    tcp_port_arg = DeclareLaunchArgument(
        'tcp_port',
        default_value='10000',
        description='TCP Port for Unity connection'
    )

    # ROS TCP Endpoint ノード
    server_endpoint_node = Node(
        package='ros_tcp_endpoint',
        executable='default_server_endpoint',
        name='server_endpoint',
        parameters=[{
            'tcp_ip': tcp_ip,
            'tcp_port': tcp_port
        }],
        emulate_tty=True
    )

    # Mover ノード
    mover_node = Node(
        package='utra6_moveit',
        executable='mover.py',
        name='mover',
        output='screen'
    )

    # === 【修正箇所】MoveItの実機起動 ===
    moveit_config_share = get_package_share_directory('utra6_550_moveit_config')
    
    # demo.launch.py ではなく、実機用の起動設定を探して使います。
    # 一般的に MoveIt Setup Assistant で作ったパッケージなら、demo.launch.py に引数を渡すことで解決する場合もありますが、
    # Umbratekの公式例にならって 'utra_moveit.launch.py' があると仮定します。
    # もしエラーが出る場合は、ファイル名を 'demo.launch.py' に戻し、引数を追加する方法に切り替えます。
    
    # 試してほしいファイル名候補:
    # 1. utra_moveit.launch.py (推奨)
    # 2. moveit_planning_execution.launch.py
    # 3. demo.launch.py (引数で use_fake_hardware:=false が効く場合あり)

    launch_file_name = 'demo.launch.py' # いったんdemoのまま、引数で実機化を試みます

    real_robot_launch = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            os.path.join(moveit_config_share, 'launch', launch_file_name)
        ),
        # ここが最重要：実機のIPを指定し、フェイクモードをオフにする
        launch_arguments={
            'robot_ip': '192.168.11.164',       # ロボットのIP
            'use_fake_hardware': 'false',      # 偽物をオフ
            'load_robot_description': 'true'   # モデルをロード
        }.items()
    )

    return LaunchDescription([
        tcp_ip_arg,
        tcp_port_arg,
        server_endpoint_node,
        mover_node,
        real_robot_launch, # 修正版のlaunch
    ])
