@Echo off
echo args = [%1] [%2]

IF ([%1]==[] OR [%2]==[]) (
  echo Err: missing zip file and zip tool file!
) ELSE (
  rmdir /S /Q .\e3tools_update_unzip
  %2 a -r e3tools_backup.zip 
  timeout 1
  %2 x -aoa -y -bb3 -o.\e3tools_update_unzip %1  
  timeout 1
  xcopy /E /Y /R .\e3tools_update_unzip\. .
  timeout 1
  rmdir /S /Q .\e3tools_update_unzip  
  timeout 1
  start e3tools.exe
)
