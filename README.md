# Float From Skin Server
Works as server to https://chrome.google.com/webstore/detail/float-from-skin/nlheooelajnkbnpgjojileplpjhkcham

# Why
Since you can't make the request for the skin information from the browser you need an external server to work as a proxy between your browser and the Steam servers.  
You could use a webserver hosted by me but that would cost me money. Also, Steam blocks you if you make more than one request per 1,4 seconds so I would need to host a lot of servers and/or make the application take a long time to respond and by the time you received your response the skin would have already been sold.  
So I decided to make a very small app to work as a proxy and this is it.

# How it works
This is a very simple console application that receives requests from your browser over HTTP (port 8001 by default but you can easily change it).  
It then asks the Steam Servers for information about the skin. To do this it need to be logged in a Steam Account. Because Steam is very strict about receiving too many requests per second and it will block this app temporarily (less than 5min) the app waits 1.4 seconds between requests to prevent it.  
After it received the information on the skin it sends that information to your browser.

# Account to use
You can't be logged in in this app and on the Official Steam Client at the same time (it might be possible but I don't know how to do it yet).  
You also need to have CS GO on the account you are going to use for this app.  
Which is why you will probably want to use this app with and alt account.

# Security
This app is able to keep you logged in between sessions but it doesn't remember your password (just like the Official Steam Client).

# Setup
1. Download and place anywhere you want in your PC: https://github.com/Overhatted/FloatFromSkin/releases/download/v1.0/FloatFromSkin.exe
2. Execute
3. Enter the Username of your account
4. Enter the Password of your account
5. Enter the SteamGuard code if necessary
6. To exit it press ctrl+c
