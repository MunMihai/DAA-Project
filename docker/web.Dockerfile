FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY Quiz.Web/app/nginx.conf /etc/nginx/conf.d/default.conf
COPY .artifacts/web/ /usr/share/nginx/html

EXPOSE 8080
