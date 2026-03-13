cmake_minimum_required(VERSION 3.8)
project(utra6_moveit)

if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# 依存パッケージの検索
find_package(ament_cmake REQUIRED)
find_package(rclpy REQUIRED)
find_package(std_msgs REQUIRED)
find_package(geometry_msgs REQUIRED)
find_package(moveit_msgs REQUIRED)
find_package(sensor_msgs REQUIRED)
find_package(rosidl_default_generators REQUIRED)

# カスタムメッセージ・サービスの生成
# 注意: msg/srvフォルダ内のファイル名は元のままと仮定しています
#rosidl_generate_interfaces(${PROJECT_NAME}
#  "msg/Utra6MoveitJoints.msg"
#  "msg/Utra6Trajectory.msg"
#  "srv/MoverService.srv"
#  DEPENDENCIES geometry_msgs std_msgs moveit_msgs sensor_msgs
#)

# Pythonスクリプトのインストール
# ROS 2では lib/<package_name> に実行可能ファイルを置くのが一般的です
install(PROGRAMS
  scripts/mover.py
  DESTINATION lib/${PROJECT_NAME}
)

# LaunchファイルやConfigのインストール
install(DIRECTORY launch 
  DESTINATION share/${PROJECT_NAME}
)

ament_package()
