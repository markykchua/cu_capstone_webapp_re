# Background info 
Reverse Engineering a Web application. 

Let us create a math format for the problem we want to resolve. Denote the web application under reverse engineering as a function $f(x)$ in which an instance of $x$ is an API. Denote the complete set of APIs for a particular $f$ as $\Alpha$. We may know a subset of $\Alpha$. The task is to implement an automated method to  find the complete $\Alpha$ or at least increase the size of the known API set. 

## Related tools/projects
- MITMProxy2Swagger: https://github.com/alufers/mitmproxy2swagger 
- POSTMAN: https://www.postman.com
- Zaproxy: https://github.com/zaproxy/zaproxy
- Restler-fuzzer: https://github.com/microsoft/restler-fuzzer?tab=readme-ov-file
- Katana: https://github.com/projectdiscovery/katana
- LinkFinder https://github.com/GerbenJavado/LinkFinder
- FFUF https://github.com/ffuf/ffuf

## Tentative features to be delivered by the project
- discover undocumented APIs
- discover the business logic via the APIs discovered.
- Orchestrate the API calls to automate a user journey

## Tentative work items - based on the known info of the target web app
1. All the APIs are fully known (this is the strongest assumption): orchestrate sequences of API calls to simulate a user. The component for compiling APIs specs into python calls in Restler-fuzzer can be borrowed or reused.
2. APIs are unknown but web traffic can be captured in the client side such as a HAR file: leverage tools such as MITMProxy2Swagger to build API specs and it falls back to case 1 if ```all``` APIs can be found. This is untrue for most of cases.
3. In most cases and the most difficult case we only know part of APIs either via public API specs or from traffic. We need to create new methods to obtain more API specs. It is a task similar to fuzzing but with a much larger space to search. 

# The team
We operate to build a Minimal Viable Product. Imagine we are 4 friends trying to build something that can lead to a product. 

The team is supposed to be cross-functional. 
- The project mentors: as like a product owner. Their responsibilities include: defining product features and providing suggestions/guidance on technical and non-technical issues
- Each of the students work like an individual contributor in a team in an enterprise setting. Most of the impelmentations will be done by the students.

So we need sufficient motivation to keep working.
What motivate us? 

# Meeting notes
## Sept 20 action item:
1. Implemement: Record and Replay
To build an end-to-end scenario in which it will automate the following: 
- Capture the http traffic (the user needs to manually interact with the target app - login, post etc.)
- Derive the API specs
- Orchestrate sequences of calls. And these sequences are user journey. 
Question: to what extend could the tool reproduce results consistently over different target apps? Would it work against apps with Bot detection enabled? 

2. Test against 20 web apps, percentage of apps can be record-n-replay? 

3. Compare and contrast of existing tools/projects

Note: C# is to be considered by the team. 

