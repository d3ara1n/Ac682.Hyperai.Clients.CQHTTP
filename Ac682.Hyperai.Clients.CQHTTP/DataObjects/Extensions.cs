using Hyperai.Relations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP.DataObjects
{
    public static class Extensions
    {
        public static Member ToMember(this DtoGroupSender target, Group group)
        {
            Member member = new Member()
            {
                DisplayName = string.IsNullOrEmpty(target.Card) ? target.Nickname : target.Card,
                Group = new Lazy<Group>(group),
                Identity = target.User_Id,
                Title = target.Title,
                Nickname = target.Nickname,
                Role = OfRole(target.Role),
            };
            return member;
        }

        public static GroupRole OfRole(string name)
        {
            return name switch
            {
                "owner" => GroupRole.Owner,
                "admin" => GroupRole.Administrator,
                "member" => GroupRole.Member,
                _ => GroupRole.Member
            };
        }

        public static Friend ToFriend(this DtoFriendSender target)
        {
            Friend friend = new Friend()
            {
                Nickname = target.Nickname,
                Identity = target.User_Id,
                Remark = target.Nickname,
            };
            return friend;
        }
    }
}
