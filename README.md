# Ac682.Hyperai.Clients.CQHTTP

实际对标的是 go-cqhttp 而不是 cqhttp.

这是一座💩🗻，会在 onebot v12 之后重构

## 部署

同隔壁 [Mirai Adapter](https://github.com/ac682/Ac682.Hyperai.Clients.Mirai), `Options` 只需要填 `Host`, `HttpPort`, `WebSocketPort`, `AccessToken` 就行。

**依赖 Wupoo 包**

从 nuget 上下载一个然后丢进 `plugins` 文件夹就行了

## 实现与未实现

- [x] 大部分
- [x] MessageId 获取
- [x] 小部分
- [ ] 临时消息 *Hyperai根本没有这部分接口*


### 接收事件

- [x] 私聊消息
- [x] 群聊消息
- [x] 群消息撤回
- [x] 私聊消息撤回
- [x] 自己/群员被踢/离开
- [x] 群员被禁言
- [x] 群员改名片
- [x] 群成员加入
- [x] 被邀请加入群
- [x] 被申请添加好友
- [ ] 其他一概未知(未测出来

### 发送事件

- [x] 私聊消息
- [x] 群聊消息
- [x] 私聊消息撤回
- [x] 群聊消息撤回
- [x] 退群/踢人
- [x] 禁言/解禁
- [x] 全员禁言
- [x] 设置群名片
- [x] 设置群名
- [x] 通过好友请求
- [x] 通过群邀请请求
- [ ] 其他所有

## 消息元素

- [x] Plain
- [x] Image
- [x] Face
- [x] Flash
- [x] At
- [x] AtAll
- [x] Quote
- [x] Source
- [x] Voice
- [x] Video
- [x] Music
- [x] Node *Node在收发的时候都保证可用, 隐藏了不完全的Forward by id和Node by id*
- [ ] 所有 ContentBase 派生
- [x] 有什么不懂就塞给 Unknown