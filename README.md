# Float From Skin Server
Works as server to https://chrome.google.com/webstore/detail/float-from-skin/nlheooelajnkbnpgjojileplpjhkcham

# Why
Since you can't make the request for the skin information from the browser you need an external server to work as a proxy between your browser and the Steam servers.  
You can use the public server hosted on floatfromskin.tryrecords.com but if you want to host your own server you can use this app. Steam blocks you if you make more than one request per 1.4 seconds so the ammount of Steam Clients available in the public server to make requests for the skins' float value might not be enough.  

# How it works
This is a very simple console application that receives requests from your browser over HTTP (port 8001 by default but you can easily change it).  
It then asks the Steam Servers for information about the skin. To do this it needs to be logged in a Steam Account. Because Steam is very strict about receiving too many requests per second and it will not respond to some requests unless the app waits 1.4 seconds between requests.  
After it received the information on the skin it sends that information to your browser.

# Account to use
You need to have CS GO on the account you are going to use for this app.  

# Security
This app is able to keep you logged in between sessions but it doesn't remember your password (just like the Official Steam Client).

# Setup
1. Download and place anywhere you want in your PC: https://github.com/Overhatted/FloatFromSkin/releases/download/v1.0/FloatFromSkin.exe
2. Execute
3. Go to http://localhost:8001
4. Enter the Username of your account
5. Enter the Password of your account
6. Enter the SteamGuard code if necessary
7. Click Submit
8. To stop the app press ctrl+c

# Build from source
1. Download and install Visual Studio (https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx)
2. Click "Download ZIP" on top of this page
3. Extract the ZIP anywhere you want
4. Open FloatFromSkin.sln
5. Click F6
6. Your executable is now in FloatFromSkin/bin/Debug/FloatFromSkin.exe

# Linux
Get the latest version of mono (http://www.mono-project.com/)  
Run:  
`mono FloatFromSkin.exe`
