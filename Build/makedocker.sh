#!/bin/bash

source ./Build/utils.sh

DOCKER_TAG="$DOCKERHUB_USER/$DOCKERHUB_REPO:$VERSION-$ARCH"

echo "Docker tag: $DOCKER_TAG"
sudo docker build --pull -t "$DOCKER_TAG" -f "$DOCKERFILE" .

sudo docker login --username=$DOCKERHUB_USER --password=$DOCKERHUB_PASS
sudo docker push "$DOCKER_TAG"