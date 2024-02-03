FROM node:20-bookworm

RUN apt-get update && apt-get install -y curl git
RUN git clone https://github.com/misskey-dev/misskey.git --depth 1 -b master
WORKDIR /misskey
RUN npm install -g pnpm
RUN pnpm install
RUN pnpm build
RUN cp /misskey/.config/example.yml /misskey/.config/default.yml
RUN pnpm --filter backend run generate-api-json
RUN chmod 777 /misskey/packages/backend/built/api.json