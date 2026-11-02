@echo off
REM Thesis 02/11: dataset v8 collection with bot v6.1 teachers (same personas/schedules as v6). 520 matches, about 20 minutes.
REM Needs the build WITH bot v6.1. Runs from its own folder.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/3] Synthetic8_main (300)
python run_parallel.py --exe "%EXE%" --schedule schedule_main_v6 --out Synthetic8_main --n 4 --merge
echo [2/3] Synthetic8_extreme (60)
python run_parallel.py --exe "%EXE%" --schedule schedule_extreme_pilot_v6 --out Synthetic8_extreme --n 4 --merge
echo [3/3] Synthetic8_ext (160)
python run_parallel.py --exe "%EXE%" --schedule schedule_ext_v6 --out Synthetic8_ext --n 4 --merge
echo ALL DONE
pause
