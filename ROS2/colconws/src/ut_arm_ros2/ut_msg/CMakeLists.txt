cmake_minimum_required(VERSION 3.8)
project(ut_msg)

if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

find_package(ament_cmake REQUIRED)
find_package(rosidl_default_generators REQUIRED)
find_package(geometry_msgs REQUIRED)
find_package(std_msgs REQUIRED)
find_package(moveit_msgs REQUIRED)
find_package(sensor_msgs REQUIRED)
find_package(trajectory_msgs REQUIRED)

# フォルダ内のすべてのmsgとsrvファイルを自動で取得
file(GLOB msg_files RELATIVE "${CMAKE_CURRENT_SOURCE_DIR}" "msg/*.msg")
file(GLOB srv_files RELATIVE "${CMAKE_CURRENT_SOURCE_DIR}" "srv/*.srv")

# インターフェース生成
rosidl_generate_interfaces(${PROJECT_NAME}
  ${msg_files}
  ${srv_files}
  DEPENDENCIES
    geometry_msgs
    std_msgs
    moveit_msgs
    sensor_msgs
    trajectory_msgs
)

ament_export_dependencies(rosidl_default_runtime)
ament_package()
