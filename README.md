# Netipam

Netipam is a UniFi-centric, self-hosted IP address management and network visibility tool designed for networks from homelabs to small business.
No cloud required, installs easily in Docker or Linux.

More information on Netipam can be found at https://netipam.com

# Installation

## Linux (Ubuntu/Debian)

Installer provides options to install, update or uninstall 

`wget https://github.com/nodeplex/Netipam/releases/download/v0.8.6/install-netipam.sh`<br>
`sudo bash install-netipam.sh`<br>


## Docker

Prereqs: Docker + Docker Compose.

#### Create a folder and add the compose file:

  If you store your Docker containers in a different folder, adjust the `/opt` below accordingly
  
  `mkdir -p /opt/netipam`<br>
  Save docker-compose.yml in /opt/netipam<br>

#### Start the container:

  `cd /opt/netipam`<br>
  `docker compose up -d`<br>

#### Open the app:

`http://<host-ip>:7088`

## Synology (Container Manager)

Create a folder on the NAS:<br>
Example: /volume1/docker/netipam<br>

Place docker-compose.yml in that folder (or you can upload when creating the project).

Open Container Manager → Projects → Create.<br>

Select the folder and compose file (or upload it), then deploy.<br>

Open Netipam:<br> 
`http://<nas-ip>:7088`<br>

### Port choice

The default port is **7088** to avoid clashing with common web ports (80/443/8080).  
You can change it in `docker-compose.yml`

Or change the internal port with `ASPNETCORE_URLS` if you want Netipam to listen on a different port inside the container.

More information on Netipam can be found at https://netipam.com
