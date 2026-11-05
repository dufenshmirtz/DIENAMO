@echo off
REM Thesis 05/11: RL cycle 3 - style reward (needs the build with ILRemotePolicy protocol 2). beta 0.2 as cycle 2.
REM Two 3-hour runs one after the other: style weight 1.0, then 2.0. Models every 5 updates. About 6 hours.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/2] RL fine-tuning beta 0.2, style weight 1.0 -^> RLc3_w1
python rl_finetune.py --exe "%EXE%" --schedule schedule_rl_train_v2 --init ..\models\theta8h_q0_e60_joint.json --out RLc3_w1 --n 4 --hours 3 --beta 0.2 --style-weight 1.0 --export-every 5
echo [2/2] RL fine-tuning beta 0.2, style weight 2.0 -^> RLc3_w2
python rl_finetune.py --exe "%EXE%" --schedule schedule_rl_train_v2 --init ..\models\theta8h_q0_e60_joint.json --out RLc3_w2 --n 4 --hours 3 --beta 0.2 --style-weight 2.0 --export-every 5
echo ALL DONE
pause
