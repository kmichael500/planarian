# Planarian

Planarian is a web app designed to help you manage your cave project data. Built with .NET Core and React with TypeScript, Planarian is fast and reliable, making it easy for you to keep track of your projects, trips, and objectives.

Some key features of Planarian include:

-   Unlimited projects: With Planarian, you can create an unlimited number of projects to keep track of your work. Each project can contain multiple trips and objectives, giving you the flexibility to organize your data however you like.
    
-   Team member management: You can invite people to your projects or individual objectives, allowing you to collaborate with others on specific parts of your work. This makes it easy to keep track of who is working on what.
    
-   Photos, trip reports, and tags: Objectives in Planarian can contain photos from team members, trip reports, and tags to help you organize and categorize your data. Some of the tags available include Survey, Re-survey, Photography, Biology, and Archeology, making it easy to filter and find the information you need.
    
-   Lead tracking: One unique feature of Planarian is the ability to keep track of leads within objectives. You can add leads manually or extract them from TH2 files using the "continuation" symbol in digital sketching apps like TopoDroid. For each lead, you can record details like the description, classification (Good, Decent, Bad, Unknown), and date found, making it easy to keep track of all your leads in one place.
    

Overall, Planarian is an intuitive and powerful tool for managing your cave project data. Its features make it easy to stay organized and on top of your work, allowing you to focus on what matters most: your cave exploration and research. Try Planarian today and see how it can streamline your cave project management!

```
**Client config**

Configure appsettings.Template.Development.json in ./Planarian/Planarian/ with the appropriate connection strings.

Rename appsettings.Template.Development.json to appsettings.Development.json

**Starting the client**

Open ./Planarian/Planarian.sln in Visual Studio

Run the client

**Starting the server**
``` bash
# Navigate to ./Planarian.Web
# Start running the server
npm start
```

Your browser should automatically open Planarian to the login screen.
