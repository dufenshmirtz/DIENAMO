@echo off
REM Thesis 04/11: RL cycle 2 - tighter KL leash. Two 3-hour runs one after the other (beta 0.2, then 0.1).
REM Schedule v2: Aris styles P00N / P00A / P00M (P00D left out: forced play). Models every 5 updates. About 6 hours.
cd /d "%~dp0"
set EXE=C:\Users\arish\Documents\DIENAMO\IL_Mult_Scene\HoH.exe
echo [1/2] RL fine-tuning beta 0.2 -^> RLp_b02
python rl_finetune.py --exe "%EXE%" --schedule schedule_rl_train_v2 --init ..\models\theta8h_q0_e60_joint.json --out RLp_b02 --n 4 --hours 3 --beta 0.2 --export-every 5
echo [2/2] RL fine-tuning beta 0.1 -^> RLp_b01
python rl_finetune.py --exe "%EXE%" --schedule schedule_rl_train_v2 --init ..\models\theta8h_q0_e60_joint.json --out RLp_b01 --n 4 --hours 3 --beta 0.1 --export-every 5
echo ALL DONE
pause
