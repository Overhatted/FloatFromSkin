# Float From Skin Server
Works as server to:  
Chrome extension: https://chrome.google.com/webstore/detail/float-from-skin/nlheooelajnkbnpgjojileplpjhkcham  
Firefox extension: https://addons.mozilla.org/en-US/firefox/addon/float-from-skin/  
This is the same software I use in the public server at floatfromskin.tryrecords.com.  
You can see statistics about the public server at http://floatfromskin.tryrecords.com.

# Why
Since you can't make the request for the skin information from the browser you need an external server to work as a proxy between your browser and the Steam servers.  
Steam throttles requests very aggressively so this app can only make one request every 1.4 seconds per steam account. This means we need a lot of steam accounts with a Counter-Strike: Global Offensive copy each.

# Contribute
If you want to contribute you can donate a copy of Counter-Strike: Global Offensive to overhatted@gmail.com which is also the email I use to publish the Chrome Web Store extension. The public server is currently located in the European Union region (Ireland).

# How it works
This is a very simple console application that receives requests from your browser over Web Sockets (port 8000 by default but you can easily change it).  
It then asks the Steam Servers for information about the skin. To do this it needs to be logged in a Steam Account with Counter-Strike: Global Offensive.  
After it receives the information on the skin it sends that information to your browser.  
It then caches the skin's float value in case the same skin is requested again.  
You can control the steam accounts used by the app at http://localhost:8001.
The admin key can be found written in the console.

# Security
This app is able to keep you logged in between sessions but it doesn't remember your password (just like the Official Steam Client).

# Host your own server
1. Download and place anywhere you want in your PC: https://github.com/Overhatted/FloatFromSkin/releases/download/v1.1.2/FloatFromSkin.exe
2. Execute
3. Copy the key from the console (on Windows: select the key with your mouse and then press enter to copy it into the clipboard)
4. Go to http://localhost
5. Enter the key and press enter to login
6. Enter the Username of the account you want to use
7. Enter the Password of the account you want to use
8. Enter the SteamGuard code if necessary
9. Click Submit
10. To stop the app press ctrl+c

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

# Public server
Currently using an EC2 linux t2.micro instance in us-west-2 (Oregon) as part of the free tier which expires in August 2017.
