import venv
import os
import sys
import subprocess

def create_and_install():
    env_dir = os.path.join(os.path.dirname(__file__), "venv")
    print(f"Creating venv at {env_dir}...")
    
    # Create the virtual environment
    builder = venv.EnvBuilder(with_pip=True)
    builder.create(env_dir)
    print("Venv created successfully.")
    
    # Path to the new pip
    pip_exe = os.path.join(env_dir, "Scripts", "pip.exe")
    
    if not os.path.exists(pip_exe):
        print(f"Error: Could not find pip at {pip_exe}")
        return

    python_exe = os.path.join(env_dir, "Scripts", "python.exe")
    if not os.path.exists(python_exe):
        print(f"Error: Could not find python at {python_exe}")
        return

    print("Upgrading pip, setuptools, and wheel...")
    try:
        subprocess.check_call([
            python_exe,
            "-m",
            "pip",
            "install",
            "--upgrade",
            "pip",
            "setuptools",
            "wheel",
        ])
        print("Packaging tools upgraded successfully.")
    except subprocess.CalledProcessError as e:
        print(f"Failed to upgrade packaging tools: {e}")
        return
        
    print("Installing requirements...")
    req_file = os.path.join(os.path.dirname(__file__), "requirements.txt")
    
    # Install dependencies including psycopg2-binary
    try:
        subprocess.check_call([pip_exe, "install", "-r", req_file, "psycopg2-binary"])
        print("Dependencies installed successfully.")
    except subprocess.CalledProcessError as e:
        print(f"Failed to install dependencies: {e}")

if __name__ == "__main__":
    create_and_install()
