import requests
from bs4 import BeautifulSoup
import argparse
import json  # Import json for safer parsing
import urllib.parse  # Import for decoding URL-encoded strings
import httpx  # Import httpx for HTTP/2 support


#BASE_URL = "https://www.automationexercise.com"
#LOGIN_EMAIL = "endpointexplorer@gmail.com"
#LOGIN_PASSWORD = "EE2025!@"

# Session to persist cookies
session = requests.Session()

# get the CSRF token from the login page
def get_csrf_token(base_url):
    login_url = f"{base_url}"
    response = session.get(login_url)
    print(f"GET {login_url} - Response: {response.status_code}")
    print(response.text)  

    soup = BeautifulSoup(response.text, 'html.parser')
    
    # Debugging: Print the HTML to inspect the login form
    with open("login_page.html", "w", encoding="utf-8") as f:
        f.write(soup.prettify())
    
    # Extract the CSRF token from the login form, adjusting the name if needed
    csrf_token = soup.find('input', {'name': 'csrfmiddlewaretoken'})  
    if csrf_token:
        return csrf_token['value']
    else:
        print("CSRF token not found. Check the login page HTML.")
        exit()

# login funtion
def login(base_url, identifier, password, method):
    login_url = f"{base_url}"
    
    # Get the CSRF token
    csrf_token = get_csrf_token(base_url)
    
    # Decode the identifier and password
    decoded_identifier = urllib.parse.unquote_plus(identifier)
    decoded_password = urllib.parse.unquote_plus(password)
    
    # Determine if the identifier is an email or username
    if "@" in decoded_identifier:
        login_payload = {
            'email': decoded_identifier,  # Use the identifier as email
            'password': decoded_password,
            'csrfmiddlewaretoken': csrf_token  
        }
    else:
        login_payload = {
            'username': decoded_identifier,  # Use the identifier as username
            'password': decoded_password,
            'csrfmiddlewaretoken': csrf_token 
        }
    
    # Set headers, including the Referer header
    headers = {
        'Referer': login_url  # Include the Referer header
    }

    # Send login request
    if method == "GET":
        login_response = session.get(login_url, data=login_payload, headers=headers)
    elif method == "POST":
        login_response = session.post(login_url, data=login_payload, headers=headers)
    
    #print(f"POST {login_url} - Response: {login_response.status_code}")
    #print(login_response.text)  # Print the response for debugging

    print(login_payload)
    if "Logged in as" in login_response.text:
        print("Login successful!")
    else:
        print("Login failed. Check your credentials.")
        exit()

# Function to parse the JSON file
def parse_request_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as file:
        data = json.load(file)

    # Extract FlowElements and ExternalVariables
    flow_elements = data.get("FlowElements", [])
    external_variables = data.get("ExternalVariables", {})

    # Log the extracted variables for debugging
    print("\n=== External Variables ===")
    for key, value in external_variables.items():
        print(f"{key}: {value}")

    return flow_elements, external_variables

# Function to replay requests from the parsed data
def replay_requests(file_path):
    flow_elements, external_variables = parse_request_file(file_path)

    with httpx.Client(http2=True) as client:
        for element in flow_elements:
            request = element.get("Request", {})
            response = element.get("Response", {})

            method = request.get("Method")
            if method == 0:
                method = "GET"
            elif method == 1:
                method = "POST"
            url = request.get("Url")

            headers = request.get("Headers", {})
            # Filter out pseudo-headers (headers starting with ':')
            headers = {key: value for key, value in headers.items() if not key.startswith(":")}
            #headers = {key: value for key, value in headers.items() if key not in [":authority", ":method"]}
            body = request.get("Body", None)

            # Update session cookies
            cookies = external_variables.get("Cookies", {})
            #client.cookies.update(cookies)

            print(f"\nReplaying Request: {method} {url}")
            print(f"Headers: {headers}")
            print(f"Body: {body}")

            # Send the request
            if method == "GET":
                response_obj = client.get(url, headers=headers)
            elif method == "POST":
                response_obj = client.post(url, headers=headers, data=body)
            else:
                print(f"Unsupported method: {method}")
                continue

            print(f"Response Status: {response_obj.status_code}")
            print(f"Response Body: {response_obj.text[:500]}")  # Print first 500 characters of the response

            # Compare the actual response with the expected response (if provided)
            expected_status = response.get("Status")
            if expected_status and response_obj.status_code != expected_status:
                print(f"Expected Status: {expected_status}, but got: {response_obj.status_code}")

# Function to replay requests step by step
def replay_requests_step_by_step(file_path):
    flow_elements, external_variables = parse_request_file(file_path)

    for i, element in enumerate(flow_elements):
        request = element.get("Request", {})
        response = element.get("Response", {})

        method = request.get("Method")
        if method == 0:
            method = "GET"
        elif method == 1:
            method = "POST"
        url = request.get("Url")
        headers = request.get("Headers", {})
        headers = {key: value for key, value in headers.items() if not key.startswith(":")}
        body = request.get("Body", None)

        print(f"\nStep {i + 1}: Replaying Request: {method} {url}")
        print(f"Headers: {headers}")
        print(f"Body: {body}")

        if "login" in url or "signin" in url:
            login(url, external_variables["extracted_email_"], external_variables["extracted_password_"], method)
            continue

        if method == "GET":
            response_obj = session.get(url, headers=headers)
        elif method == "POST":
            response_obj = session.post(url, headers=headers, data=body)
        else:
            print(f"Unsupported method: {method}")
            continue

        print(f"Response Status: {response_obj.status_code}")
        print(f"Response Body: {response_obj.text[:500]}")  # Print first 500 characters of the response

        # Pause for user input before proceeding to the next request
        input("\nPress Enter to continue to the next request...")

# CLI menu
def display_menu():
    file_path = None
    external_variables = {}

    while True:
        if not file_path:
            print("\nNo file selected. Please select a JSON file to proceed.")
            file_path = input("Enter the path to the JSON file: ")
            try:
                _, external_variables = parse_request_file(file_path)
                for key, value in external_variables.items():
                    if key in ("extracted_email_", "extracted_password_", "extracted_username_"):
                        external_variables[key] = urllib.parse.unquote_plus(value)
            except Exception as e:
                print(f"Error loading file: {e}")
                file_path = None
                continue

        print("\n=== Main Menu ===")
        print(f"Current file: {file_path}")
        print("1. Replay requests from the selected JSON file")
        print("2. Replay requests step by step from the selected JSON file")
        print("3. Modify external variables")
        print("4. Change JSON file")
        print("5. Exit")
        choice = input("Enter your choice: ")

        if choice == "1":
            replay_requests(file_path)
        elif choice == "2":
            replay_requests_step_by_step(file_path)
        elif choice == "3":
            modify_external_variables(external_variables)
        elif choice == "4":
            file_path = None  # Reset file selection
        elif choice == "5":
            print("Exiting the program. Goodbye!")
            break
        else:
            print("Invalid choice. Please try again.")

# Modify external variables CLI
def modify_external_variables(external_variables):
    print("\n=== Modify External Variables ===")
    print("Current external variables:")
    for key, value in external_variables.items():
        print(f"{key}: {value}")

    while True:
        print("\nOptions:")
        print("1. Modify a variable")
        print("2. Add a new variable")
        print("3. Delete a variable")
        print("4. Return to main menu")
        choice = input("Enter your choice: ")

        if choice == "1":
            print(external_variables)
            key = input("Enter the name of the variable to modify: ")
            if key in external_variables:
                new_value = input(f"Enter the new value for {key}: ")
                external_variables[key] = new_value
                print(f"Variable '{key}' updated to '{new_value}'.")
            else:
                print(f"Variable '{key}' not found.")
        elif choice == "2":
            key = input("Enter the name of the new variable: ")
            value = input(f"Enter the value for {key}: ")
            external_variables[key] = value
            print(f"Variable '{key}' added with value '{value}'.")
        elif choice == "3":
            key = input("Enter the name of the variable to delete: ")
            if key in external_variables:
                del external_variables[key]
                print(f"Variable '{key}' deleted.")
            else:
                print(f"Variable '{key}' not found.")
        elif choice == "4":
            break
        else:
            print("Invalid choice. Please try again.")

if __name__ == "__main__":
    display_menu()