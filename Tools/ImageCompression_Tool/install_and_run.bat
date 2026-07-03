@echo off
chcp 65001 >nul
echo ========================================
echo  图片压缩工具 - 安装并启动
echo ========================================
echo.

:: 检查 Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未检测到 Python，请先安装 Python 3.8+
    echo 下载地址: https://www.python.org/downloads/
    pause
    exit /b 1
)

echo [1/2] 安装依赖...
pip install -r requirements.txt --quiet
if errorlevel 1 (
    echo [错误] 依赖安装失败，请检查网络或手动运行: pip install Pillow
    pause
    exit /b 1
)

echo [2/2] 启动程序...
echo.
python app.py

pause
