FROM node:20-bookworm

RUN apt-get update && apt-get install -y curl git
RUN git clone https://github.com/misskey-dev/misskey.git --depth 1 -b master
WORKDIR /misskey
RUN git submodule update --init --recursive
RUN npm install -g pnpm
RUN pnpm install
RUN cp /misskey/.config/example.yml /misskey/.config/default.yml
ENTRYPOINT ["/bin/sh", "-c" , "pnpm --filter misskey-reversi run build && pnpm build-pre && pnpm --filter backend run generate-api-json && cp packages/backend/built/api.json ./.docker-tmp"]