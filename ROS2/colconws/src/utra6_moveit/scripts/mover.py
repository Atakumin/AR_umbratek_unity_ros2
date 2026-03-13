#!/usr/bin/env python3

import rclpy
import copy
import math
import time
import sys

from rclpy.node import Node
from rclpy.action import ActionClient
from rclpy.executors import MultiThreadedExecutor
from rclpy.callback_groups import ReentrantCallbackGroup

# ★修正: JointConstraint を追加インポート
from moveit_msgs.msg import RobotTrajectory, RobotState, Constraints, PositionConstraint, OrientationConstraint, JointConstraint
from moveit_msgs.action import MoveGroup
from sensor_msgs.msg import JointState
from geometry_msgs.msg import PoseStamped, Pose
from shape_msgs.msg import SolidPrimitive
from trajectory_msgs.msg import JointTrajectoryPoint
from moveit_msgs.srv import GetCartesianPath
from std_srvs.srv import SetBool 

from ut_msg.srv import MoverService
from ut_msg.msg import Utra6Trajectory
from ut_msg.srv import SetEnable, SetInt16, MovetoJointP2p

class MoverNode(Node):
    def __init__(self):
        super().__init__('utra6_moveit_server')

        self.callback_group = ReentrantCallbackGroup()
        
        self.declare_parameter('group_name', 'Arm')
        self.group_name = self.get_parameter('group_name').get_parameter_value().string_value
        self.joint_names = ['joint1', 'joint2', 'joint3', 'joint4', 'joint5', 'joint6']
        self.end_effector_link = "link_6"

        # ===============================================================================
        # ★★★ ズレ補正 (単位: 度 Degree) ★★★
        # Joint 3 を -90度 補正したい場合 -> [0.0, 0.0, -90.0, 0.0, 0.0, 0.0]
        # ===============================================================================
        self.joint_offsets = [0.0, 0.0, 90.0, 0.0, 90.0, 0.0] 
        # ===============================================================================

        self.latest_trajectories = [] 

        self._action_client = ActionClient(self, MoveGroup, 'move_action', callback_group=self.callback_group)
        self.cartesian_client = self.create_client(GetCartesianPath, 'compute_cartesian_path', callback_group=self.callback_group)
        self.srv = self.create_service(MoverService, '/ut_msg/MoverService', self.plan_pick_and_place_callback, callback_group=self.callback_group)
        self.exec_srv = self.create_service(SetBool, '/ut_msg/ExecuteTrajectory', self.execute_last_plan_service_callback, callback_group=self.callback_group)

        self.cli_enable = self.create_client(SetEnable, '/utarm/apisrv/set_motion_enable', callback_group=self.callback_group)
        self.cli_status = self.create_client(SetInt16, '/utarm/apisrv/set_motion_status', callback_group=self.callback_group)
        self.cli_move = self.create_client(MovetoJointP2p, '/utarm/apisrv/moveto_joint_p2p', callback_group=self.callback_group)

        self.get_logger().info("MoverNode Ready (Home Return Added)")

    def convert_robot_trajectory_to_unity_msg(self, robot_traj):
        unity_traj = Utra6Trajectory()
        if robot_traj is None or robot_traj.joint_trajectory is None:
            return unity_traj
        unity_traj.trajectory = robot_traj.joint_trajectory
        return unity_traj

    def set_orientation_down(self, pose):
        new_pose = copy.deepcopy(pose)
        new_pose.orientation.x = 1.0
        new_pose.orientation.y = 0.0
        new_pose.orientation.z = 0.0
        new_pose.orientation.w = 0.0
        return new_pose

    def get_offset_pose(self, pose, z_offset):
        offset_pose = copy.deepcopy(pose)
        offset_pose.position.z += z_offset
        return offset_pose

    async def execute_last_plan_service_callback(self, request, response):
        if request.data:
            if not self.latest_trajectories:
                response.success = False
                response.message = "No trajectories found."
                return response
            
            if not self.cli_enable.service_is_ready() or not self.cli_move.service_is_ready():
                response.success = False
                response.message = "Robot services are not ready."
                self.get_logger().error(response.message)
                return response

            self.get_logger().info("Starting Execution...")

            try:
                # 1. Enable Robot
                req_enable = SetEnable.Request()
                req_enable.axis = 8
                req_enable.enable = 1
                await self.cli_enable.call_async(req_enable)
                time.sleep(0.5)

                # 2. Set Status
                req_status = SetInt16.Request()
                req_status.data = 0
                await self.cli_status.call_async(req_status)
                time.sleep(0.5)

                # 3. Execute Trajectories
                for i, traj in enumerate(self.latest_trajectories):
                    
                    points = traj.joint_trajectory.points
                    final_point = points[-1]
                    
                    # A. MoveIt計算値 (ラジアン)
                    raw_rad_joints = list(final_point.positions)
                    
                    # B. MoveIt計算値 (度)
                    deg_joints = [math.degrees(val) for val in raw_rad_joints]

                    # C. オフセット計算 (度 + 度)
                    corrected_deg_joints = []
                    for j in range(len(deg_joints)):
                        val = deg_joints[j] + self.joint_offsets[j]
                        corrected_deg_joints.append(val)

                    # D. 実機送信値 (度 -> ラジアン変換)
                    final_rad_joints = [math.radians(val) for val in corrected_deg_joints]

                    self.get_logger().info(f"--- Segment {i+1} Details ---")
                    self.get_logger().info(f"  1. MoveIt (Rad) : {['{:.3f}'.format(x) for x in raw_rad_joints]}")
                    self.get_logger().info(f"  2. Offset (Deg) : {['{:.1f}'.format(x) for x in corrected_deg_joints]}")
                    self.get_logger().info(f"  3. Sending (Rad): {['{:.3f}'.format(x) for x in final_rad_joints]}")

                    # --- 実機への送信 (ラジアン) ---
                    req_move = MovetoJointP2p.Request()
                    req_move.joints = final_rad_joints
                    req_move.speed = 0.5  
                    req_move.acc = 1.0    

                    future = self.cli_move.call_async(req_move)
                    result = await future
                    
                    self.get_logger().info(f"  Result: {result}")
                    time.sleep(4.0)

                response.success = True
                response.message = "Execution Complete."
                self.get_logger().info("ALL DONE.")
                self.latest_trajectories = []

            except Exception as e:
                response.success = False
                response.message = f"Service Call Failed: {e}"
                self.get_logger().error(response.message)
        else:
            response.success = False
            response.message = "Cancelled."

        return response

    async def plan_cartesian_path(self, target_pose, start_joints_list):
        if not self.cartesian_client.wait_for_service(timeout_sec=1.0): return None, 0.0
        req = GetCartesianPath.Request()
        req.header.frame_id = "world"
        req.header.stamp = self.get_clock().now().to_msg()
        req.group_name = self.group_name
        req.link_name = self.end_effector_link
        start_state = RobotState()
        joint_state = JointState()
        joint_state.name = self.joint_names
        joint_state.position = [float(j) for j in start_joints_list]
        start_state.joint_state = joint_state
        req.start_state = start_state
        req.waypoints = [target_pose]
        req.max_step = 0.01
        req.jump_threshold = 0.0
        req.avoid_collisions = True
        future = self.cartesian_client.call_async(req)
        response = await future
        if response.error_code.val != 1: return None, 0.0
        return response.solution, response.fraction

    async def plan_standard_path(self, target_pose, start_joints_list):
        if not self._action_client.wait_for_server(timeout_sec=1.0): return None
        goal_msg = MoveGroup.Goal()
        goal_msg.request.group_name = self.group_name
        goal_msg.request.num_planning_attempts = 10
        goal_msg.request.allowed_planning_time = 5.0
        goal_msg.request.max_velocity_scaling_factor = 0.5
        goal_msg.request.max_acceleration_scaling_factor = 0.5
        goal_msg.planning_options.plan_only = True
        start_state = RobotState()
        joint_state = JointState()
        joint_state.name = self.joint_names
        joint_state.position = [float(j) for j in start_joints_list]
        start_state.joint_state = joint_state
        goal_msg.request.start_state = start_state
        
        # Pose Constraints
        constraints = Constraints()
        constraints.name = "goal_constraints"
        pos_constraint = PositionConstraint()
        pos_constraint.header.frame_id = "world"
        pos_constraint.link_name = self.end_effector_link
        tolerance_sphere = SolidPrimitive()
        tolerance_sphere.type = SolidPrimitive.SPHERE
        tolerance_sphere.dimensions = [0.005] 
        pos_constraint.constraint_region.primitives.append(tolerance_sphere)
        pos_constraint.constraint_region.primitive_poses.append(target_pose)
        pos_constraint.weight = 1.0
        constraints.position_constraints.append(pos_constraint)
        ori_constraint = OrientationConstraint()
        ori_constraint.header.frame_id = "world"
        ori_constraint.link_name = self.end_effector_link
        ori_constraint.orientation = target_pose.orientation 
        ori_constraint.absolute_x_axis_tolerance = 0.1
        ori_constraint.absolute_y_axis_tolerance = 0.1
        ori_constraint.absolute_z_axis_tolerance = 0.1 
        ori_constraint.weight = 1.0
        constraints.orientation_constraints.append(ori_constraint)
        goal_msg.request.goal_constraints.append(constraints)

        send_goal_future = self._action_client.send_goal_async(goal_msg)
        goal_handle = await send_goal_future
        if not goal_handle.accepted: return None
        result = await goal_handle.get_result_async()
        if result.result.error_code.val != 1: return None
        return result.result.planned_trajectory

    # --- ★追加: 関節角度をターゲットにする計画関数 ---
    async def plan_joint_path(self, target_joints, start_joints_list):
        if not self._action_client.wait_for_server(timeout_sec=1.0): return None
        goal_msg = MoveGroup.Goal()
        goal_msg.request.group_name = self.group_name
        goal_msg.request.num_planning_attempts = 10
        goal_msg.request.allowed_planning_time = 5.0
        goal_msg.request.max_velocity_scaling_factor = 0.5
        goal_msg.request.max_acceleration_scaling_factor = 0.5
        goal_msg.planning_options.plan_only = True
        
        # Start State
        start_state = RobotState()
        joint_state = JointState()
        joint_state.name = self.joint_names
        joint_state.position = [float(j) for j in start_joints_list]
        start_state.joint_state = joint_state
        goal_msg.request.start_state = start_state
        
        # Goal Constraints (Joints)
        constraints = Constraints()
        constraints.name = "joint_goal"
        
        for i, joint_name in enumerate(self.joint_names):
            jc = JointConstraint()
            jc.joint_name = joint_name
            jc.position = float(target_joints[i])
            jc.tolerance_above = 0.01
            jc.tolerance_below = 0.01
            jc.weight = 1.0
            constraints.joint_constraints.append(jc)
            
        goal_msg.request.goal_constraints.append(constraints)

        send_goal_future = self._action_client.send_goal_async(goal_msg)
        goal_handle = await send_goal_future
        if not goal_handle.accepted: return None
        result = await goal_handle.get_result_async()
        if result.result.error_code.val != 1: return None
        return result.result.planned_trajectory

    async def smart_plan(self, target_pose, start_joints_list, phase_name="Move"):
        self.get_logger().info(f"[{phase_name}] Trying Linear Path...")
        traj, fraction = await self.plan_cartesian_path(target_pose, start_joints_list)
        if traj is None or fraction < 0.9:
            self.get_logger().warn(f"[{phase_name}] Linear failed. Switching to Standard.")
            traj = await self.plan_standard_path(target_pose, start_joints_list)
        else:
            self.get_logger().info(f"[{phase_name}] Linear Path Success!")
        return traj

    async def plan_pick_and_place_callback(self, request, response):
        self.latest_trajectories = []
        
        # ★全体の計算開始時間を記録
        overall_start_time = time.time()
        
        # Unityからのリクエストに含まれる「開始時の関節角度」をHomeとする
        home_joints = list(request.joints_input.joints)
        
        pick_pose = self.set_orientation_down(request.pick_pose)
        place_pose = self.set_orientation_down(request.place_pose)
        pre_pick_pose = self.get_offset_pose(pick_pose, 0.15)
        pre_place_pose = self.get_offset_pose(place_pose, 0.15)
        
        current_joints = list(request.joints_input.joints)
        
        phase_times = [] # 各フェーズの時間を格納するリスト

        try:
            # 各フェーズの実行関数をリスト化してループで回すと計測しやすいですが、
            # 既存の構造を活かして、各ステップの前後で計測します。

            # 1. Pre-Pick
            t_start = time.time()
            traj_1 = await self.smart_plan(pre_pick_pose, current_joints, "1. Pre-Pick")
            phase_times.append(time.time() - t_start)
            if not traj_1: raise Exception("Failed: Pre-Pick")
            response.trajectories.append(self.convert_robot_trajectory_to_unity_msg(traj_1))
            self.latest_trajectories.append(traj_1) 
            current_joints = traj_1.joint_trajectory.points[-1].positions

            # 2. Pick
            t_start = time.time()
            traj_2 = await self.smart_plan(pick_pose, current_joints, "2. Pick")
            phase_times.append(time.time() - t_start)
            if not traj_2: raise Exception("Failed: Pick")
            response.trajectories.append(self.convert_robot_trajectory_to_unity_msg(traj_2))
            self.latest_trajectories.append(traj_2) 
            current_joints = traj_2.joint_trajectory.points[-1].positions

            # 3. Pre-Place
            t_start = time.time()
            traj_3 = await self.smart_plan(pre_place_pose, current_joints, "3. Pre-Place")
            phase_times.append(time.time() - t_start)
            if not traj_3: raise Exception("Failed: Pre-Place")
            response.trajectories.append(self.convert_robot_trajectory_to_unity_msg(traj_3))
            self.latest_trajectories.append(traj_3) 
            current_joints = traj_3.joint_trajectory.points[-1].positions

            # 4. Place
            t_start = time.time()
            traj_4 = await self.smart_plan(place_pose, current_joints, "4. Place")
            phase_times.append(time.time() - t_start)
            if not traj_4: raise Exception("Failed: Place")
            response.trajectories.append(self.convert_robot_trajectory_to_unity_msg(traj_4))
            self.latest_trajectories.append(traj_4) 
            current_joints = traj_4.joint_trajectory.points[-1].positions

            # 5. Return to Home
            t_start = time.time()
            traj_5 = await self.plan_joint_path(home_joints, current_joints)
            phase_times.append(time.time() - t_start)
            if not traj_5: raise Exception("Failed: Return Home")
            response.trajectories.append(self.convert_robot_trajectory_to_unity_msg(traj_5))
            self.latest_trajectories.append(traj_5)

            # ★集計結果の表示
            overall_duration = time.time() - overall_start_time
            self.get_logger().info("-------------------------------------------")
            self.get_logger().info(f"PLANNING SUCCESS (Total Calculation: {overall_duration:.3f}s)")
            self.get_logger().info(f" 1.Pre-Pick: {phase_times[0]:.3f}s")
            self.get_logger().info(f" 2.Pick    : {phase_times[1]:.3f}s")
            self.get_logger().info(f" 3.Pre-Plac: {phase_times[2]:.3f}s")
            self.get_logger().info(f" 4.Place   : {phase_times[3]:.3f}s")
            self.get_logger().info(f" 5.Home    : {phase_times[4]:.3f}s")
            self.get_logger().info("-------------------------------------------")

        except Exception as e:
            self.get_logger().error(f"Planning Error: {e}")
            self.latest_trajectories = [] 
        return response

def main(args=None):
    rclpy.init(args=args)
    mover_node = MoverNode()
    executor = MultiThreadedExecutor()
    executor.add_node(mover_node)
    try:
        executor.spin()
    except KeyboardInterrupt:
        pass
    finally:
        mover_node.destroy_node()
        rclpy.shutdown()

if __name__ == "__main__":
    main()