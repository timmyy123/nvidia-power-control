$b = [System.IO.File]::ReadAllBytes('c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe')
$s = [System.Text.Encoding]::Unicode.GetString($b)
[regex]::Matches($s, '[\u0020-\u007E]{10,200}') | ForEach-Object { $_.Value } | Where-Object { $_ -match 'offset|bounds|Enable|unclickable|disabled|Settings|voltage' } | Select-Object -Unique -First 30
