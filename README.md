# Float From Skin Server
Works as server to https://chrome.google.com/webstore/detail/float-from-skin/nlheooelajnkbnpgjojileplpjhkcham
This is the same software I use in the public server at floatfromskin.tryrecords.com.  
You can learn to host your own in Setup section.

# Costs with public server


# Why
Since you can't make the request for the skin information from the browser you need an external server to work as a proxy between your browser and the Steam servers.  
Steam blocks you if you make more than one request per 1.4 seconds so the ammount of Steam Clients available in the public server to make requests for the skins' float value might not be enough.

# How it works
This is a very simple console application that receives requests from your browser over Web Sockets (port 8000 by default but you can easily change it).  
It then asks the Steam Servers for information about the skin. To do this it needs to be logged in a Steam Account with Counter-Strike: Global Offensive.  
After it received the information on the skin it sends that information to your browser.  
It then caches the skin's float value in case the same skin is requested again.  
You can control the accounts at http://localhost:8001.

# Security
This app is able to keep you logged in between sessions but it doesn't remember your password (just like the Official Steam Client).

# Setup
1. Download and place anywhere you want in your PC: https://github.com/Overhatted/FloatFromSkin/releases/download/v1.0/FloatFromSkin.exe
2. Execute
3. Copy the key from the console (on Windows: select the key with your mouse and then press enter to copy it into the clipboard)
4. Go to http://localhost:8001
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
