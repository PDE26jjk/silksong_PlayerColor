import os
import shutil
import subprocess
import time
import psutil
import sys

def main():
    # 配置区域 - 根据你的环境修改这些路径
    target_directory = r"R:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins"
    steam_game_id = "1030300"

    print("=" * 50)
    print("Hollow Knight Silksong 部署脚本")
    print("=" * 50)

    # 检查命令行参数
    if len(sys.argv) < 2:
        print("错误: 请提供源DLL路径作为参数")
        return
    
    source_dll = sys.argv[1]
    print(f"\n使用源DLL文件: {source_dll}")

    # 步骤1: 关闭正在运行的 Hollow Knight Silksong
    print("\n[步骤1] 检查游戏进程...")
    closed = False
    for proc in psutil.process_iter(['name']):
        if proc.info['name'] == "Hollow Knight Silksong.exe":
            try:
                print(f"发现运行中的游戏进程 (PID: {proc.pid})")
                proc.kill()
                print("游戏进程已终止")
                closed = True
                time.sleep(1)  # 等待进程完全关闭
            except Exception as e:
                print(f"游戏进程出错: {str(e)}")

    if not closed:
        print("未检测到运行中的游戏进程")

    # 步骤2: 复制DLL文件
    print("\n[步骤2] 复制模组文件...")
    try:
        if not os.path.exists(source_dll):
            raise FileNotFoundError(f"源DLL文件不存在: {source_dll}")

        if not os.path.exists(target_directory):
            raise FileNotFoundError(f"目标目录不存在: {target_directory}")

        shutil.copy2(source_dll, target_directory)
        print(f"成功复制: {os.path.basename(source_dll)} -> {target_directory}")
    except Exception as e:
        print(f"复制文件时出错: {str(e)}")
        return

    # 步骤3: 通过Steam启动游戏
    print("\n[步骤3] 启动游戏...")
    steam_url = f"steam://rungameid/{steam_game_id}"
    try:
        subprocess.Popen(["start", steam_url], shell=True)
        print(f"已通过Steam启动游戏 (ID: {steam_game_id})")
    except Exception as e:
        print(f"启动游戏时出错: {str(e)}")
        print("请手动通过Steam启动游戏")


if __name__ == "__main__":
    main()