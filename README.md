# CareTogetherCMS
CareTogether is an open-source case management system (CMS) for nonprofits connecting families to caring communities. [The wiki provides much more detail about the design of CareTogether.](https://github.com/CareTogether/CareTogetherCMS/wiki) If you prefer to jump straight into the code, keep reading!

![License](https://badgen.net/github/license/CareTogether/CareTogetherCMS)
![Available Good First Issues](https://badgen.net/github/label-issues/CareTogether/CareTogetherCMS/good%20first%20issue/open)

## Contributing
Thank you for your interest in helping to build this vital tool! If you'd like to jump straight into development, please check out the **[issues labeled "Help Wanted"](https://github.com/CareTogether/CareTogetherCMS/issues?q=is%3Aopen+is%3Aissue+label%3A%22help+wanted%22+-label%3Ablocked+-label%3A%22needs+spec%22)**, which are tasks that have been identified as a good fit for initial contributions and have documentation to support you. They are intentionally not critically time-sensitive, so you can work on them at your own pace. We are tracking the overall status of those issues in [the CareTogether Contributions project](https://github.com/orgs/CareTogether/projects/2/views/1).

If you have additional time to dedicate to contributing and feel you're ready to take on larger chunks of feature development work, please contact [Lars Kemmann](https://github.com/LarsKemmann) to set up an introductory call and request an invite to Teams where we are coordinating the design, development, and support efforts for CareTogether.

### Collaboration Guidelines
We ask that you practice effective communication, preferably through comments on the GitHub issues:

1. Let others know that you are interested in working on an issue by **leaving a comment.**
2. As soon as you start working on an issue, **create a *draft* pull request** for it so that others can see any progress you have made.
3. **Push changes to your PR frequently!** We prefer small incremental commits over large changes that you keep locally for several days, so that others can see your progress.
4. Once you've completed developing *and testing* your changes locally, **publish your draft PR** so that maintainers will know to begin a PR review.
5. **Stay responsive to comments** on your PR.

## Prerequisites
You can build and run CareTogether on any operating system supported by Node.js and .NET, including Windows, MacOS, and Linux. Install the following:
- [Node.js 18 LTS](https://nodejs.org/en/download)
- [.NET 8 LTS](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Visual Studio Code](https://code.visualstudio.com/Download) (recommended)

You will also need to allow PowerShell scripts to run on your computer (see [documentation](https://learn.microsoft.com/en-us/previous-versions//bb613481(v=vs.85)?redirectedfrom=MSDN#how-to-allow-scripts-to-run)). To do this, open an administrative PowerShell session and run the following command:
`Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`

## Development
_**NOTE:** The API project comes with a basic set of test data for local development. This test data is automatically regenerated each time you start the API project._

1. Clone the repository into any local directory on your device.
2. Run the _CareTogether.Api_ project and the React client.
   * If using **Visual Studio Code** (recommended), all you need to do is open the repository folder in VS Code and hit 'F5' to start debugging _both_ the client and server. VS Code will also automatically install and run the Azurite emulator for Azure Storage in your project folder.
   * If using the command line, you will need `dotnet run` (in the _src/CareTogether.Api_ directory) to start the API, and `npm run dev` (in the _src/caretogether-pwa_ directory) to start the client. If using another IDE such as Visual Studio, use the equivalent debug or launch options.
3. To sign into the application's local test environment, use the following credentials:
   - Administrator
      - Email Address: `test@bynalogic.com`
      - Password: `P@ssw0rd`
   - Volunteer
      - Email Address: `test2@bynalogic.com`
      - Password: `P@ssw0rd`

### Troubleshooting Your Local Setup
1. If pressing 'F5' does not start the project for you in **Visual Studio Code**, then you likely have a key-mapping issue with your keyboard & will need to instead select "Start Debugging" from **Visual Studio Code**'s "Run" menu
2. If the server side of the application does not start correctly and/or seems to be having issues that you're having trouble debugging, you can try running the "CareTogether.Api" project separately in a different IDE, such as Visual Studio
3. If you're having problems getting the Azurite emulator to run (such as a "No connection could be made because the target machine actively refused it" error on the "tenantContainer.CreateIfNotExists()" method of "TestStorageHelper.cs"), you may need to install & run the Azurite emulator manually. To do so:
- Run ```npm install -g azurite``` from the command line
- Navigate to the local repository where you cloned the project
- Run the ```azurite-blob --loose``` command to start Azurite from the command line (This will run Azurite with the default Blob service endpoint as we don't use the Queue or Table storage endpoints currently. You can also add a `--silent` parameter if you don't want to see individual requests logged to the terminal. The `--loose` parameter is currently required to support valet key access from the browser.)

## Licensing Notice
CareTogether is licensed under the GNU Affero General Public License v3.0 (AGPL-3.0) which, crucially, **only permits hosting this software (including derivatives) if** you also make the source code of the software and any of your modifications available to your users under this same license. This effectively ensures that CareTogether CMS remains forever open-source and doesn't simply become the base code for a proprietary derivative at some point. We value collaboration and openness, and we believe that the best way to accomplish this is to ensure the software remains open to everyone.
### Licensing Exemption
The CareTogether name and logo are exempt from the AGPL-3.0 license as they remain the property of Coept LLC. Among other things, this means that you are not allowed to brand or advertise your hosted version of this software as being the CareTogether CMS without express written permission from Coept LLC. This is intended both to protect the business interest of Coept LLC in providing the CareTogether hosted service and to protect against impersonation of the CareTogether hosted service by third parties.
