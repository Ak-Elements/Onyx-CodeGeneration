cd /d "%~dp0"

cmake -S . -B out
cmake --build out --config Release --target install