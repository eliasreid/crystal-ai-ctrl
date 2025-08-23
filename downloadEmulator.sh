if [ -d "BizHawk" ]; then
  echo "BizHawk folder already exists. Remove if you want to download a new copy."
  exit 1
fi

curl -LO "https://github.com/TASEmulators/BizHawk/releases/download/2.10/BizHawk-2.10-win-x64.zip"

mkdir ./BizHawk

unzip ./BizHawk-2.10-win-x64.zip -d BizHawk
