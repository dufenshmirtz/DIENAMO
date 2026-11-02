@echo off
REM Thesis 02/11: Step 0, pressure test pilot (theta6h2 vs theta6). 2 runs x 80 matches, about 15 minutes. Runs from its own folder.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/2] Pressure pilot theta6h2
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_pilot --out Pressure_theta6h2 --n 4 --ilmodel ..\models\theta6h2_joint.json --merge
echo [2/2] Pressure pilot theta6
python run_parallel.py --exe "%EXE%" --schedule schedule_pressure_pilot --out Pressure_theta6 --n 4 --ilmodel ..\models\theta6_joint.json --merge
echo ALL DONE
pause
