@echo off
REM Thesis 02/11: bot v6 pilot. 4 runs, about 20-25 minutes. Runs from its own folder. Needs the build WITH bot v6.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/4] Bot pilot v6 (knob personas as v6 vs REF v5)
python run_parallel.py --exe "%EXE%" --schedule schedule_bot_pilot30_v6 --out Botpilot_v6 --n 4 --merge
echo [2/4] Bot pilot v5 check (same schedule as Botpilot_v5: v5 must be unchanged by the new code)
python run_parallel.py --exe "%EXE%" --schedule schedule_bot_pilot30 --out Botpilot_v5check --n 4 --merge
echo [3/4] Pressure v6 with theta6h2
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_v6 --out Pressure6_theta6h2 --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo [4/4] Pressure v6 with theta6h
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_v6 --out Pressure6_theta6h --n 4 --ilmodel ..\models\theta6h_joint.json --merge
echo ALL DONE
pause
